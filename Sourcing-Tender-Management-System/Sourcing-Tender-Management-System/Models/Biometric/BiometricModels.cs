namespace Sourcing_Tender_Management_System.Models.Biometric;

/// <summary>Settings for biometric sign-in (section "Biometric" in configuration; every value has a default).</summary>
public class BiometricOptions
{
    /// <summary>WebAuthn relying-party id. Default: the host name of the request (for example "localhost").</summary>
    public string? RpId { get; set; }

    /// <summary>Origins a WebAuthn response may come from. Default: the origin of the request.</summary>
    public string[]? Origins { get; set; }

    public string RpName { get; set; } = "Sourcing & Tender Management System";

    /// <summary>Largest allowed Euclidean distance between a live face descriptor and an enrolled one.</summary>
    public double FaceMatchThreshold { get; set; } = 0.5;

    /// <summary>How long a one-time face challenge stays valid.</summary>
    public int ChallengeSeconds { get; set; } = 90;

    /// <summary>
    /// How long a one-time fingerprint (passkey) challenge stays valid. Longer than the face one,
    /// because using a phone through the QR code (scan, unlock, confirm) takes a while.
    /// </summary>
    public int PasskeyChallengeSeconds { get; set; } = 300;

    public int MaxFaceTemplatesPerUser { get; set; } = 10;
    public int MaxPasskeysPerUser { get; set; } = 10;

    /// <summary>Failed biometric attempts allowed per IP address / account name inside the window.</summary>
    public int MaxFailuresPerWindow { get; set; } = 10;

    public int FailureWindowMinutes { get; set; } = 10;
}

/// <summary>Relying-party details for one request.</summary>
public record RpInfo(string RpId, string[] Origins, string RpName);

public class BiometricResult
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }

    /// <summary>The account the verification belongs to (sign-in flows).</summary>
    public ApplicationUser? User { get; set; }

    public static BiometricResult Fail(string error) => new() { Error = error };
    public static BiometricResult Ok(ApplicationUser? user = null) => new() { Succeeded = true, User = user };
}

// ---------- Fingerprint / device passkey (WebAuthn) ----------

public class PasskeyRegisterRequest
{
    public string Token { get; set; } = string.Empty;

    /// <summary>Credential id (base64url).</summary>
    public string Id { get; set; } = string.Empty;

    public string ClientDataJson { get; set; } = string.Empty;
    public string AttestationObject { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public class PasskeyLoginRequest
{
    public string Token { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string ClientDataJson { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? UserHandle { get; set; }
    public string? ReturnUrl { get; set; }
}

// ---------- Face recognition with liveness ----------

/// <summary>One measurement of the live video: eye openness, head yaw and mouth openness.</summary>
public class FaceFrame
{
    /// <summary>Milliseconds since the challenge started.</summary>
    public double T { get; set; }

    public double Ear { get; set; }
    public double Yaw { get; set; }
    public double Mar { get; set; }
}

public class FaceDescriptorSample
{
    public double T { get; set; }

    /// <summary>The 128-number face descriptor produced in the browser.</summary>
    public float[] V { get; set; } = [];
}

public class FaceSubmission
{
    public string Token { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<FaceFrame> Frames { get; set; } = [];
    public List<FaceDescriptorSample> Descriptors { get; set; } = [];

    /// <summary>Enrolment only: replace the templates already stored instead of adding to them.</summary>
    public bool Replace { get; set; }

    public string? ReturnUrl { get; set; }
}

/// <summary>Limits shared by the browser (to guide the user) and the server (to verify the evidence).</summary>
public class LivenessThresholds
{
    public double EarClosed { get; set; } = 0.20;
    public double EarOpen { get; set; } = 0.26;
    public double YawCenter { get; set; } = 0.08;
    public double YawTurn { get; set; } = 0.15;
    public double MarClosed { get; set; } = 0.20;
    public double MarOpen { get; set; } = 0.40;
}

public class FaceChallengeResponse
{
    public string Token { get; set; } = string.Empty;
    public string[] Actions { get; set; } = [];
    public int ExpiresInSeconds { get; set; }
    public LivenessThresholds Thresholds { get; set; } = new();
}

// ---------- Manage page ----------

public class BiometricManageViewModel
{
    public List<PasskeyRow> Passkeys { get; set; } = [];
    public int FaceTemplateCount { get; set; }
}

public class PasskeyRow
{
    public long Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
