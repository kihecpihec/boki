using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Boki.Editor
{
    public sealed class MovementChecks : ICallbacks
    {
        private static TestRunnerApi runner;

        [MenuItem("Boki/Player Model/Run EditMode Tests")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running EditMode tests.");
            if (runner != null)
                throw new InvalidOperationException("A test run is already active.");
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new MovementChecks());
            runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            string path = Path.GetFullPath("Logs/movement-editmode.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            TestRunnerApi.SaveResultToFile(result, path);
            Debug.Log($"MOVEMENT_EDITMODE: {result.PassCount} passed, {result.FailCount} failed; {path}");
            runner.UnregisterCallbacks(this);
            UnityEngine.Object.DestroyImmediate(runner);
            runner = null;
        }
    }
}
