using UnityEngine;
using System.Collections;
public class Radar : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Gang_Enemy gang;

    [SerializeField]
    private float loseTargetDelay = 3f;
    Vector2 returnPf;
    private Coroutine loseTargetCoroutine;

    private void Awake()
    {
        gang = GetComponentInParent<Gang_Enemy>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        gang.playerObj = other.gameObject;
        gang.player = other.transform;
        returnPf = gang.transform.position;

        if (loseTargetCoroutine != null)
        {
            StopCoroutine(loseTargetCoroutine);
            loseTargetCoroutine = null;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        loseTargetCoroutine = StartCoroutine(LoseTarget());
    }

    private IEnumerator LoseTarget()
    {
        yield return new WaitForSeconds(loseTargetDelay);

        gang.playerObj = null;
        gang.player = null;
        gang.returnP = returnPf;
    }
}
