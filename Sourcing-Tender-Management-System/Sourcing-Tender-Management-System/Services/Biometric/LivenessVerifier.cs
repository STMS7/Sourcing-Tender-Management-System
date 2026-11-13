using System.Security.Cryptography;
using Sourcing_Tender_Management_System.Models.Biometric;

namespace Sourcing_Tender_Management_System.Services.Biometric;

/// <summary>
/// Checks that the face measurements really show a live person doing the steps the server asked
/// for, in order: a blink, opening the mouth, turning the head. A photo or a still frame cannot do
/// these, which is why file uploads are never accepted.
///
/// Limitation, stated plainly: the measurements are produced by the browser, so a determined
/// attacker who forges the whole request could fake them. For accounts that need the strongest
/// protection the device fingerprint (WebAuthn) is the safer method; this check stops casual
/// spoofing such as holding up a photo or replaying an old request.
/// </summary>
public static class LivenessVerifier
{
    public const string Blink = "blink";
    public const string OpenMouth = "openMouth";
    public const string TurnHead = "turnHead";

    private static readonly string[] AllActions = [Blink, OpenMouth, TurnHead];

    /// <summary>Two different steps in a random order, so a recorded session cannot be replayed.</summary>
    public static string[] PickActions()
    {
        var actions = (string[])AllActions.Clone();
        for (var i = actions.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (actions[i], actions[j]) = (actions[j], actions[i]);
        }

        return actions[..2];
    }

    /// <summary>Returns null when the evidence is valid, otherwise the reason it is not.</summary>
    public static string? Verify(string[] actions, IReadOnlyList<FaceFrame> frames, LivenessThresholds t)
    {
        if (actions.Length == 0 || frames.Count < 10 || frames.Count > 1200)
        {
            return "Not enough video evidence.";
        }

        var previous = double.MinValue;
        foreach (var f in frames)
        {
            if (!double.IsFinite(f.T) || !double.IsFinite(f.Ear) || !double.IsFinite(f.Yaw) || !double.IsFinite(f.Mar) ||
                f.T < previous || f.T < 0 || f.T > 180_000 ||
                f.Ear is < 0 or > 1 || f.Mar is < 0 or > 3 || Math.Abs(f.Yaw) > 2)
            {
                return "The video evidence is not valid.";
            }

            previous = f.T;
        }

        if (frames[^1].T - frames[0].T < 1500)
        {
            return "The session was too short.";
        }

        var start = 0;
        foreach (var action in actions)
        {
            if (!TryFind(action, frames, t, start, out var end))
            {
                return $"The step '{action}' was not detected.";
            }

            if (frames[end].T - frames[start].T > 30_000)
            {
                return "A step took too long.";
            }

            start = end + 1;
        }

        return null;
    }

    // Each step is a short sequence of states that must be seen one after the other, close together in time.
    private static bool TryFind(string action, IReadOnlyList<FaceFrame> frames, LivenessThresholds t, int start, out int end)
    {
        end = -1;
        Func<FaceFrame, bool>[] states;
        double maxSpanMs;

        switch (action)
        {
            case Blink:
                states = [f => f.Ear >= t.EarOpen, f => f.Ear <= t.EarClosed, f => f.Ear >= t.EarOpen];
                maxSpanMs = 2500;
                break;
            case OpenMouth:
                states = [f => f.Mar <= t.MarClosed, f => f.Mar >= t.MarOpen];
                maxSpanMs = 4000;
                break;
            case TurnHead:
                states = [f => Math.Abs(f.Yaw) <= t.YawCenter, f => Math.Abs(f.Yaw) >= t.YawTurn];
                maxSpanMs = 4000;
                break;
            default:
                return false;
        }

        var state = 0;
        double firstT = 0;
        for (var i = start; i < frames.Count; i++)
        {
            var frame = frames[i];

            if (state > 0 && frame.T - firstT > maxSpanMs)
            {
                state = 0;
            }

            if (states[state](frame))
            {
                if (state == 0)
                {
                    firstT = frame.T;
                }

                state++;
                if (state == states.Length)
                {
                    end = i;
                    return true;
                }
            }
        }

        return false;
    }
}
