using UnityEngine;

public class BulletSFX : MonoBehaviour
{
    public AudioSource source;

    public AudioClip grappleFire;
    public AudioClip grappleLatched;
    public AudioClip grappleMissed;
    public AudioClip grappleReleased;
    public AudioClip ricochet;
    public AudioClip death;

    void OnEnable()
    {
        var bm = GetComponent<BulletMovement>();
        bm.onGrappleFired += (pos, dir) => source.PlayOneShot(grappleFire);
        bm.onGrappleLatched += () => source.PlayOneShot(grappleLatched);
        bm.onGrappleMissed += (pos) => source.PlayOneShot(grappleMissed);
        bm.onGrappleReleased += () => source.PlayOneShot(grappleReleased);
        bm.onRicochet += (p, n) => source.PlayOneShot(ricochet);
        bm.onDeath += () => source.PlayOneShot(death);
    }
}
