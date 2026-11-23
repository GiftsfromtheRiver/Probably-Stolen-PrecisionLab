# Steam Workshop 发布工具（Precision Lab）

创意工坊页面：<https://steamcommunity.com/sharedfiles/filedetails/?id=3831705578>  
Workshop item id：`3831705578`（记录在 `item_id.txt`）

## 首次发布 / 更新版本

前置条件：
- Windows + Steam 客户端**已启动并登录**（发布账号需拥有 Probably Stolen）；
- 已安装 .NET SDK（8.0 或更高）；
- 本仓库的 `dist\PrecisionLab` 已是最新构建（含 `manifest.xml`）。

一条命令（首次创建 item；之后自动更新同一个 item）：

```powershell
powershell -ExecutionPolicy Bypass -File tools\workshop\publish.ps1 -ChangeNote "Precision Lab v1.5.0"
```

可选参数：

```powershell
# 测试上传（不出现在创意工坊公开列表）
... -Visibility unlisted

# 强制创建一个新 item（一般不需要）
... -CreateNew

# 指定游戏安装目录（默认 F:\SteamLibrary\steamapps\common\Probably Stolen）
... -GameDir "D:\Steam\steamapps\common\Probably Stolen"
```

脚本会：
1. 编译 `tools\workshop\uploader`（引用游戏自带的 Steamworks.NET 程序集）；
2. 复制游戏的 `steam_api64.dll` 并写入 `steam_appid.txt`（4348910）；
3. 以 `dist\PrecisionLab` 为内容、`preview.png` 为封面、`description.txt` 为简介，
   调用 Steam UGC 创建/更新 item；完成后把 item id 写回 `item_id.txt`。

## 修改工坊页面

- 封面：替换 `preview.png`（建议 512×512 或 16:9，512KB 内）；
- 简介：编辑 `description.txt`（支持 Steam BBCode，如 `[b]`、`[list]`）；
- 标题：在 `publish.ps1` 的 `--title` 参数里改。

## 已知说明

- 游戏内本地 `Mods\PrecisionLab` 的副本**优先级高于**创意工坊副本，所以作者自己测试时
  本地副本会“盖住”工坊版本；普通玩家订阅后直接启用即可。
- 上传完成后可运行 `WorkshopUploader.exe --query 3831705578` 检查状态（public/banned/fileSize 等）。
