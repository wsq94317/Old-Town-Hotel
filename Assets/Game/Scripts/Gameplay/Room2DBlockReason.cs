// 房间被 Blocked 时的原因。
// 以后 UI 可以根据这个原因显示“维修中”或“装修中”。
public enum Room2DBlockReason
{
    // 没有被锁定。
    None,

    // 维修，例如床坏了、空调坏了、管道问题。
    Maintenance,

    // 装修，例如翻新地板、墙纸、家具。
    Renovation
}
