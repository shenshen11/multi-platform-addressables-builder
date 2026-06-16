using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MpabBuildOrchestrator
    {
        private readonly MpabAddressablesEditorAdapter addressables = new MpabAddressablesEditorAdapter();
        private readonly MpabAddressablesGroupRuleEvaluator ruleEvaluator = new MpabAddressablesGroupRuleEvaluator();
        private readonly MpabBuildSessionStore sessionStore = new MpabBuildSessionStore();
        private readonly MpabBuildReportWriter reportWriter = new MpabBuildReportWriter();
        private readonly MpabPlatformSwitcher platformSwitcher = new MpabPlatformSwitcher();

        public MpabBuildResult Run(MultiPlatformAddressablesBuildRequest request)
        {
            var sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var report = CreateReport(sessionId, request);
            MpabBuildSessionState session = null;

            try
            {
                session = MpabBuildSessionStore.CreateNew(sessionId, request.PlatformIds, addressables);
                sessionStore.Save(session);

                foreach (var platformId in request.PlatformIds)
                {
                    var platform = request.Config.FindPlatform(platformId);
                    if (platform == null)
                        throw new InvalidOperationException($"Platform '{platformId}' does not exist.");

                    session.CurrentPlatformId = platform.PlatformId;
                    session.Step = MpabSessionStep.SwitchPlatform;
                    sessionStore.Save(session);

                    var platformReport = BuildPlatform(request, platform, session);
                    report.Platforms.Add(platformReport);

                    if (!string.Equals(platformReport.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(platformReport.ErrorMessage);

                    session.CompletedPlatforms.Add(platform.PlatformId);
                    session.PendingPlatforms.Remove(platform.PlatformId);
                    session.Step = MpabSessionStep.NextPlatform;
                    sessionStore.Save(session);
                }

                session.Step = MpabSessionStep.Restore;
                sessionStore.Save(session);
                RestoreOriginalState(request, session);
                report.RestoreSucceeded = true;

                session.Step = MpabSessionStep.GenerateReport;
                sessionStore.Save(session);
                report.Status = "Succeeded";

                var reportPath = reportWriter.Write(report, request.Config.Defaults.ReportDirectory);
                session.Step = MpabSessionStep.Done;
                sessionStore.Save(session);
                sessionStore.Clear();

                return new MpabBuildResult
                {
                    Succeeded = true,
                    Report = report,
                    ReportPath = reportPath
                };
            }
            catch (Exception ex)
            {
                MpabLogger.Error(ex.ToString());

                if (session != null)
                {
                    session.Step = MpabSessionStep.Failed;
                    session.ErrorMessage = ex.Message;
                    sessionStore.Save(session);

                    TryRestoreAfterFailure(request, session, report);
                }

                report.Status = "Failed";
                report.ErrorMessage = ex.Message;
                var reportPath = reportWriter.Write(report, request.Config.Defaults.ReportDirectory);

                return new MpabBuildResult
                {
                    Succeeded = false,
                    ErrorMessage = ex.Message,
                    Report = report,
                    ReportPath = reportPath
                };
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private MpabPlatformBuildReport BuildPlatform(
            MultiPlatformAddressablesBuildRequest request,
            MpabPlatformConfig platform,
            MpabBuildSessionState session)
        {
            var stopwatch = Stopwatch.StartNew();
            var platformReport = new MpabPlatformBuildReport
            {
                PlatformId = platform.PlatformId,
                BuildTargetName = platform.BuildTargetName,
                AddressablesProfileName = platform.AddressablesProfileName,
                Status = "Running"
            };
            var relocations = new List<MpabEntryRelocation>();

            try
            {
                EditorUtility.DisplayProgressBar("Multi Platform Addressables Builder", $"Switching platform: {platform.PlatformId}", 0.1f);
                if (!platformSwitcher.SwitchPlatform(platform, out var switchError))
                    throw new InvalidOperationException(switchError);

                session.Step = MpabSessionStep.WaitForCompilation;
                sessionStore.Save(session);
                WaitForCompilation();

                session.Step = MpabSessionStep.CheckCompilation;
                sessionStore.Save(session);
                if (EditorUtility.scriptCompilationFailed)
                    throw new InvalidOperationException($"Script compilation failed after switching platform '{platform.PlatformId}'.");

                session.Step = MpabSessionStep.ApplyAddressablesConfig;
                sessionStore.Save(session);
                if (!addressables.TrySetActiveProfile(platform.AddressablesProfileName, out var profileError))
                    throw new InvalidOperationException(profileError);

                var decisions = ruleEvaluator.Evaluate(request.Config, platform.PlatformId, request.ResourceScope, addressables.Settings.groups);
                foreach (var decision in decisions)
                {
                    if (decision.IncludeInBuild)
                        platformReport.IncludedGroups.Add(decision.GroupName);
                    else
                        platformReport.ExcludedGroups.Add(decision.GroupName);

                    if (!decision.IsMatched)
                        platformReport.UnmatchedGroups.Add(decision.GroupName);
                }

                addressables.ApplyGroupStates(decisions);

                relocations = addressables.RelocateEntriesByLabel(decisions, request.Config, platform);
                session.RelocatedEntries = relocations;

                session.Step = MpabSessionStep.SaveModifiedConfig;
                sessionStore.Save(session);
                addressables.SaveModifiedAddressablesAssets();

                session.Step = MpabSessionStep.BuildAddressables;
                sessionStore.Save(session);
                EditorUtility.DisplayProgressBar("Multi Platform Addressables Builder", $"Building Addressables: {platform.PlatformId}", 0.6f);
                var buildResult = addressables.BuildPlayerContent(request.CleanBeforeBuild);
                if (buildResult == null)
                    throw new InvalidOperationException("Addressables BuildPlayerContent returned a null result. Check the Unity Console for Addressables errors above this message.");
                if (!string.IsNullOrEmpty(buildResult.Error))
                    throw new InvalidOperationException(buildResult.Error);

                session.Step = MpabSessionStep.CollectResult;
                sessionStore.Save(session);

                if (relocations.Count > 0)
                {
                    addressables.RestoreRelocatedEntries(relocations);
                    session.RelocatedEntries.Clear();
                    sessionStore.Save(session);
                    addressables.SaveModifiedAddressablesAssets();
                }

                platformReport.Status = "Succeeded";
            }
            catch (Exception ex)
            {
                MpabLogger.Error($"Platform '{platform.PlatformId}' build failed:\n{ex}");
                platformReport.Status = "Failed";
                platformReport.ErrorMessage = ex.Message;
            }
            finally
            {
                platformReport.LabelFilteredEntries = relocations.ConvertAll(r => r.AssetPath);
                stopwatch.Stop();
                platformReport.DurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            }

            return platformReport;
        }

        private void RestoreOriginalState(MultiPlatformAddressablesBuildRequest request, MpabBuildSessionState session)
        {
            // Restore any entries still in the excluded group (non-empty only when a build failed
            // mid-platform before the per-platform restore ran).
            if (session.RelocatedEntries != null && session.RelocatedEntries.Count > 0)
            {
                addressables.RestoreRelocatedEntries(session.RelocatedEntries);
                session.RelocatedEntries.Clear();
            }

            if (request.RestoreOriginalProfileAfterBuild && !string.IsNullOrEmpty(session.OriginalAddressablesProfileName))
                addressables.TrySetActiveProfile(session.OriginalAddressablesProfileName, out _);

            if (request.RestoreOriginalGroupStatesAfterBuild)
                addressables.RestoreGroupStates(session.OriginalGroupStates);

            session.Step = MpabSessionStep.SaveRestoredConfig;
            sessionStore.Save(session);
            addressables.SaveModifiedAddressablesAssets();

            if (request.RestoreOriginalPlatformAfterBuild)
                RestoreOriginalPlatform(session);
        }

        private void TryRestoreAfterFailure(MultiPlatformAddressablesBuildRequest request, MpabBuildSessionState session, MpabBuildReport report)
        {
            try
            {
                session.Step = MpabSessionStep.Restore;
                sessionStore.Save(session);
                RestoreOriginalState(request, session);
                report.RestoreSucceeded = true;
            }
            catch (Exception restoreException)
            {
                report.RestoreSucceeded = false;
                MpabLogger.Error($"Restore after failure also failed: {restoreException}");
                MpabLogger.Error("Addressables .asset files may be in a modified state. " +
                    "Run: git checkout -- <AddressableAssetSettings.asset> and affected Group .asset files to recover.");
            }
        }

        private void RestoreOriginalPlatform(MpabBuildSessionState session)
        {
            var platform = new MpabPlatformConfig
            {
                PlatformId = session.OriginalBuildTargetName,
                SwitchMode = MpabPlatformSwitchMode.UnityBuildTarget,
                BuildTargetName = session.OriginalBuildTargetName
            };

            if (!platformSwitcher.SwitchPlatform(platform, out var error))
                MpabLogger.Warning($"Failed to restore original build target: {error}");
        }

        private static void WaitForCompilation(int timeoutSeconds = 300)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (EditorApplication.isCompiling)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"Script compilation did not finish within {timeoutSeconds} seconds.");
                System.Threading.Thread.Sleep(250);
            }
        }

        private static string ResolveAddressablesVersion()
        {
            var listRequest = Client.List(offlineMode: true);
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(50);

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var pkg in listRequest.Result)
                {
                    if (pkg.name == "com.unity.addressables")
                        return pkg.version;
                }
            }

            return "unknown";
        }

        private static MpabBuildReport CreateReport(string sessionId, MultiPlatformAddressablesBuildRequest request)
        {
            return new MpabBuildReport
            {
                SessionId = sessionId,
                BuildTimeUtc = DateTime.UtcNow.ToString("o"),
                UnityVersion = Application.unityVersion,
                AddressablesVersion = ResolveAddressablesVersion(),
                RequestedPlatforms = new List<string>(request.PlatformIds),
                ResourceScope = request.ResourceScope.ToString(),
                Status = "Running"
            };
        }
    }
}
