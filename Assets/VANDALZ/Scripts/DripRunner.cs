using UnityEngine;

// Подтёк краски на стене. Создаётся баллончиком, когда игрок долго держит его на одном месте стены.
// Каждые dripInterval секунд стекает на dripStep вниз: рисует полоску по стене
// и оставляет маленький уступ, по которому можно забираться.
// Останавливается, когда кончилась стена, упёрся в пол или сделал dripMaxSteps шагов.
public class DripRunner : MonoBehaviour
{
    PaintSpray spray;
    Vector3 surfacePoint; // текущая точка на поверхности стены
    Vector3 outward;      // нормаль стены (горизонтальная)
    Color color;
    float ledgeWidth;
    int steps;
    float timer;
    int mask;

    public void Init(PaintSpray spray, Vector3 surfacePoint, Vector3 outward, Color color, float ledgeWidth)
    {
        this.spray = spray;
        this.surfacePoint = surfacePoint;
        this.outward = outward;
        this.color = color;
        this.ledgeWidth = ledgeWidth;
        mask = ~(1 << 2); // всё, кроме игрока
    }

    void Update()
    {
        if (spray == null) { Destroy(gameObject); return; }

        timer += Time.deltaTime;
        if (timer < spray.dripInterval) return;
        timer = 0f;

        float step = spray.dripStep;

        // Упёрлись в пол (или во что-то ещё, кроме краски) — стоп.
        if (Physics.Raycast(surfacePoint + outward * 0.08f, Vector3.down, out RaycastHit floor, step + 0.05f, mask, QueryTriggerInteraction.Ignore)
            && floor.collider.GetComponent<PaintSpot>() == null)
        {
            Destroy(gameObject);
            return;
        }

        // Ниже ещё есть стена?
        Vector3 next = surfacePoint + Vector3.down * step;
        if (!Physics.Raycast(next + outward * 0.3f, -outward, out RaycastHit wall, 0.6f, mask, QueryTriggerInteraction.Ignore))
        {
            Destroy(gameObject);
            return;
        }
        if (wall.collider.GetComponent<PaintSpot>() == null) next = wall.point;

        spray.CreateDripPiece(surfacePoint, next, outward, color, ledgeWidth);
        surfacePoint = next;

        steps++;
        if (steps >= spray.dripMaxSteps) Destroy(gameObject);
    }
}
