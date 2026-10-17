using P3Makoto.Core;

int cases = 0;
void Check(bool condition, string message)
{
	if (!condition) throw new InvalidOperationException(message);
}

// Compile the production layout directly; no copied formula implementation.
foreach (float viewport in new[] { 360f, 960f, 1280f, 1920f, 2560f })
foreach (float originalScale in new[] { 0.5f, 1f, 1.5f })
foreach (float width in new[] { 315f, 420f })
for (int count = 1; count <= 32; count++)
{
	float margin = viewport * 0.06f;
	float fit = ShuffleLayout.RowScale(count, viewport, originalScale, width, margin);
	Check(fit > 0f && fit <= 1f, "Scale must only shrink the original row");
	float first = viewport / 2f - (count - 1) * ShuffleLayout.Spacing * originalScale * fit / 2f;
	float last = viewport / 2f + (count - 1) * ShuffleLayout.Spacing * originalScale * fit / 2f;
	float halfWidth = width * originalScale * fit / 2f;
	Check(first - halfWidth >= margin - 0.01f, $"Left card clipped, count={count}");
	Check(last + halfWidth <= viewport - margin + 0.01f, $"Right card clipped, count={count}");
	Check(MathF.Abs((first + last) / 2f - viewport / 2f) < 0.01f, "Row not centered");
	Check(ShuffleLayout.FlipAt(count) + 0.6f < 3f, "Safety callback can reveal before all cards land");
	cases++;
}

Check(ShuffleLayout.RowScale(0, 1920, 1, 315, 100) == 1f, "Empty reward must not divide by zero");
Check(ShuffleLayout.RowScale(3, 1920, 1, 315, 115.2f) == 1f, "Three-card layout should keep original size");
Check(ShuffleLayout.RowScale(4, 1920, 1, 315, 115.2f) == 1f, "Four-card reward should fit at full size");
Check(ShuffleLayout.FlipAt(4) > ShuffleLayout.FlipAt(3), "Fourth card must participate in landing time");
Console.WriteLine($"PASS: {cases} count/viewport/transform/width cases; empty, three-card and four-card reward checks");
