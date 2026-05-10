## Runner 快速使用

1. 打开你的玩法场景（建议 `Assets/Scenes/SampleScene.unity`）。
2. 点击菜单 `Tools/Runner/Quick Setup Current Scene`。
3. 等待脚本自动创建：
   - `RunnerGameManager`
   - `RunnerPlayer`
   - `RunnerSpawner`
   - `RunnerHUD`
   - 主相机跟随
4. 运行后操作：
   - `A/D` 或 左右方向键：换道
   - `Space` / 上方向键：跳跃
   - `S` / 下方向键：下滑
   - `R`：失败后重开

## 可选优化

- 给 `RunnerPlayer` 挂 `Animator` 并绑定你的 `Player.controller`。
- 在 `RunnerSpawner` 上配置：
  - `segmentPrefabs`：你导入的环境 prefab
  - `obstaclePrefabs`：障碍 prefab
  - `coinPrefab`：金币 prefab
- 调整 `laneOffset` 让角色车道和场景宽度匹配。
