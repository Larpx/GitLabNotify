namespace Larpx.PersonalTools.GitLabNotify.Tests;

/// <summary>
/// 测试用数据样本
/// </summary>
public static class TestSamples
{
    /// <summary>
    /// GitLab Push 事件 JSON Payload（含中文，覆盖典型场景）
    /// </summary>
    public const string PushEventPayload = """
        {
          "object_kind": "push",
          "before": "a1b2c3d4e5f6",
          "after": "f6e5d4c3b2a1",
          "ref": "refs/heads/main",
          "checkout_sha": "f6e5d4c3b2a1",
          "user_id": 42,
          "user_name": "张三",
          "user_email": "zhangsan@example.com",
          "user_avatar": "https://gitlab.com/avatar.png",
          "project_id": 100,
          "project": {
            "name": "my-project",
            "description": "测试项目",
            "web_url": "https://gitlab.example.com/mygroup/my-project",
            "avatar_url": null,
            "namespace": "mygroup",
            "visibility_level": 20,
            "path_with_namespace": "mygroup/my-project",
            "default_branch": "main",
            "homepage": "https://gitlab.example.com/mygroup/my-project",
            "url": "git@gitlab.example.com:mygroup/my-project.git",
            "ssh_url": "git@gitlab.example.com:mygroup/my-project.git",
            "http_url": "https://gitlab.example.com/mygroup/my-project.git"
          },
          "repository": {
            "name": "my-project",
            "url": "git@gitlab.example.com:mygroup/my-project.git",
            "description": "测试项目",
            "homepage": "https://gitlab.example.com/mygroup/my-project"
          },
          "commits": [
            {
              "id": "f6e5d4c3b2a1f6e5d4c3b2a1f6e5d4c3b2a1",
              "message": "fix: 修复登录问题\n\n详细描述",
              "timestamp": "2026-07-07T12:00:00Z",
              "url": "https://gitlab.example.com/mygroup/my-project/-/commit/f6e5d4c3b2a1",
              "author": { "name": "张三", "email": "zhangsan@example.com" },
              "added": ["src/new.cs"],
              "modified": ["src/old.cs"],
              "removed": []
            },
            {
              "id": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
              "message": "feat: 新增用户管理模块",
              "timestamp": "2026-07-07T11:00:00Z",
              "url": "https://gitlab.example.com/mygroup/my-project/-/commit/a1b2c3d4e5f6",
              "author": { "name": "李四", "email": "lisi@example.com" },
              "added": [],
              "modified": ["src/user.cs"],
              "removed": []
            }
          ],
          "total_commits_count": 2
        }
        """;

    /// <summary>空提交的 Push 事件</summary>
    public const string EmptyPushPayload = """
        {
          "object_kind": "push",
          "ref": "refs/heads/main",
          "user_name": "张三",
          "project": {
            "name": "my-project",
            "web_url": "https://gitlab.example.com/mygroup/my-project",
            "path_with_namespace": "mygroup/my-project"
          },
          "commits": [],
          "total_commits_count": 0
        }
        """;

    /// <summary>无效 JSON</summary>
    public const string InvalidJson = "this is not json";

    /// <summary>未知事件类型</summary>
    public const string UnknownEventPayload = """
        { "object_kind": "unknown_event", "foo": "bar" }
        """;

    /// <summary>缺少 object_kind 字段</summary>
    public const string MissingObjectKindPayload = """
        { "foo": "bar" }
        """;
}
