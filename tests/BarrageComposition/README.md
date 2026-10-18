Run from the repository root (.NET 10 SDK):

```powershell
dotnet run --project tests/BarrageComposition/BarrageComposition.csproj -- syogi/Assets/CardCatalog.asset
```

Uses the production composition source and current catalog. Covers initial values,
card addition, source preservation, count flooring, single-bullet and multi-bullet
interpolation, seconds-based power, and invalid inputs. Does not run Unity.

Unity Play Mode checks:

1. Open CardSet, select the speed card, return to the menu, and enter battle0.
   Expect eight bullets per five seconds, aimed toward the yellow center marker.
   Their speed ranges from 115 to 168 and initial power from 0.3 to 0.9.
2. Return to CardSet during the same session: the selected card and count are restored.
   Remove it and enter battle0 again: the speed range is 105 to 158.
3. Test count 8.9 (eight bullets), count 0.9, zero interval/lifetime/speed, and a speed
   offset making an endpoint nonpositive. Invalid configurations should log an error
   and emit no volley. Restore catalog values after testing.
4. Set a power slope of 0.2 and constant of 0.3: after two seconds Power should be 0.7.
5. Verify movement still uses world XY with unchanged Z, and confirm card clicks,
   scroll behavior, and hover restoration still work.

Drop checks in Unity:

- During battle0, set score to 12.5 (stageID 1). Invoke the monitor component's
  Finish Battle context menu in Play Mode. The next CardSet should list a card with
  15 parameters. Each mod is independently 137.5 * (random - 0.5) * its multiplier;
  there is no fixed sum. Speed multiplier is 0.5, so its range is -34.375 to +34.375.
- Call FinishBattle from the actual completion condition. Automatic timeout is off
  by default to preserve the existing disabled timer; enable autoFinishAfterDelay
  explicitly to use the timer.
- Repeat until full: 16 candidates maximum, oldest unselected removed, selected
  cards retained. Reopening CardSet restores selection. Restarting Play Mode resets
  the session (no disk save).
