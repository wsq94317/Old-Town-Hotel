# Edit Mode Tests

Unit-style tests that run **without entering Play Mode**. Use for:
- Pure logic (formulas, math, state machines)
- Data validation (ScriptableObject contents, enum exhaustiveness)
- Static analysis-style checks (asset references, prefab integrity)

## Running

1. Open Unity project
2. `Window → General → Test Runner`
3. Switch to the **EditMode** tab
4. **Run All** or run individually

## Adding a Test

1. Create a new C# file in this directory: `<System><Feature>Test.cs`
2. Class with `[TestFixture]`, methods with `[Test]`
3. Use `Assert.That(...)` from NUnit
4. Save — Unity auto-imports and the test appears in the Test Runner

## Example Pattern

```csharp
using NUnit.Framework;
using OldTownHotel.Gameplay; // assumes gameplay scripts are in this namespace

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DayPhaseStateMachineTest
    {
        [Test]
        public void Test_StartsInPreparation()
        {
            // 注:Story 1 落地时填补具体测试 / Note: filled in when Story 1 ships
            // var sm = new Room2DDayPhaseStateMachine();
            // Assert.That(sm.CurrentPhase, Is.EqualTo(Room2DDayPhase.Preparation));
            Assert.Pass("Placeholder — replace with real assertion when Story 1 ships.");
        }
    }
}
```

## Assembly Definition

This directory has `EditModeTests.asmdef`:
- References `UnityEngine.TestRunner` + `UnityEditor.TestRunner` + `OldTownHotel.Game`
- 游戏代码归属在 `Assets/Game/Scripts/OldTownHotel.Game.asmdef` 这个程序集里。Unity 规定 asmdef **不能反向引用 `Assembly-CSharp`**,所以游戏代码必须自己也用 asmdef 才能被测试引用
- Precompiled NUnit
- Editor-only platform (won't ship to mobile builds)
- `UNITY_INCLUDE_TESTS` define constraint — only compiled when test mode is active
