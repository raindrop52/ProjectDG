using System.IO;
using UnityEditor;

// Unity Editor가 열리거나 스크립트가 다시 컴파일될 때 Pipeline 서버 연결을 자동으로 준비합니다.
[InitializeOnLoad]
internal static class PipelineAutoStart
{
    private const string PipelineDescriptorPath = "Library/Pipeline/.unity-pipeline-port";

    static PipelineAutoStart()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess())
        {
            return;
        }

        EditorApplication.delayCall += EnsurePipelineServerStarted;
    }

    // 연결 정보 파일이 없으면 멈춘 서버 상태를 정리한 뒤 Pipeline 서버를 다시 시작합니다.
    private static void EnsurePipelineServerStarted()
    {
        if (File.Exists(PipelineDescriptorPath))
        {
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/Pipeline/Stop Server");
        EditorApplication.ExecuteMenuItem("Window/Pipeline/Start Server");
    }
}
