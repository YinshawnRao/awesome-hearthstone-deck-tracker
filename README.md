# hearthstone-deck-tracker-win

Windows 桌面端《炉石传说》记牌器。

## 原则
- 只读 `Power.log`，不读内存、不注入、不抓包
- 只显示玩家本可观察的信息（纸笔等价）
- 非官方第三方工具

## 技术栈（规划）
- C# / WPF (.NET 8)
- HearthstoneJSON / HearthDb 卡表
- 透明 Overlay + 侧窗兜底
- Velopack 分发（后续）

## 状态
骨架搭建中。
