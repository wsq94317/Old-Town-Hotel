# Play Mode Tests

Integration tests that run **in a real game scene with the full Unity runtime**. Use for:
- Cross-system flows (e.g., guest waits → patience ticks → satisfaction penalty fires)
- Coroutines and `WaitForSeconds` / `WaitForFixedUpdate`
- Scene loading and transition tests
- Anything that needs `MonoBehaviour` lifecycle methods (Awake, Start, Update)

## Running

1. Open Unity project
2. `Window → General → Test Runner`
3. Switch to the **PlayMode** tab
4. **Run All** or run individually

Play Mode tests are slower than Edit Mode tests (each test spins up the scene). Use sparingly.

## Adding a Test

1. Create `<System><Feature>PlayTest.cs` in this directory
2. Use `[UnityTest]` for coroutine-style tests; `[Test]` for synchronous tests
3. Return `IEnumerator` from `[UnityTest]` methods

## Example Pattern

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OldTownHotel.Tests.PlayMode
{
    [TestFixture]
    public class FrontDeskPatiencePlayTest
    {
        [UnityTest]
        public IEnumerator Test_PatienceCrossesImpatientThreshold_AppliesPenalty()
        {
            // 注:具体集成 story 落地时填补 / Filled when relevant integration story ships
            yield return null;
            Assert.Pass("Placeholder.");
        }
    }
}
```

## Assembly Definition

This directory has `PlayModeTests.asmdef`:
- Same setup as EditModeTests but no `Editor`-only platform restriction
- Excludes WebGL (Play Mode tests cannot run in browser builds)
- Compiles into both Editor and standalone player builds when tests are included
