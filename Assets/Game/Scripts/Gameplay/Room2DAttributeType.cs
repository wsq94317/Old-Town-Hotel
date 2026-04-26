// 房间内部属性的类型。
// 这些不是场景物体本身，而是玩法数据：UI、评价、投诉系统以后会读取它们。
public enum Room2DAttributeType
{
    // 床，例如床垫坏、床架松动。
    Bed,

    // 地板，例如太旧、破损、有异响。
    Floor,

    // 衣柜，例如柜门坏、有异响。
    Wardrobe,

    // 浴室，例如漏水、设施老旧。
    Bathroom,

    // 墙纸，例如泛黄、破损、太旧。
    Wallpaper,

    // 空调，例如噪音、制冷差。
    AirConditioner,

    // 窗户，例如漏风、隔音差。
    Window
}
