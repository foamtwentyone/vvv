using UnityEngine;

// Подтёк краски на стене. Создаётся баллончиком, когда игрок держит его на одном месте стены.
// Каждые dripInterval секунд стекает на dripStep вниз: тянет по стене узкую полоску
// (сужается книзу) и оставляет маленький уступ, по которому можно забираться.
// Останавливается, когда кончилась стена, упёрся в пол или сделал dripMaxSteps шагов.
public class DripRunner : MonoBehaviour
{
    PaintSpray spray;
    PaintStroke streak;
    Vector3 surfacePoint; // текущая точка на поверхности стены
    Vector3 outward;      // нормаль стены
    Color color;
    int steps;
    float timer;
    int solidMask;

    const float Offset = 0.035f; // чуть над основным граффити

    public void Init(PaintSpray spray, Vector3 surfacePoint, Vector3 outward, Color color)
    {
        this.spray = spray;
        this.surfacePoint = surfacePoint;
        this.outward = outward;
        this.color = color;
        solidMask = ~((1 << PaintSpray.PlayerLayer) | (1 << PaintSpray.PaintLayer));

        streak = spray.CreateLooseStroke(color);
        streak.AddPoint(surfacePoint + outward * Offset, outward, spray.dripWidth);
    }

    void Update()
    {
        if (spray == null) { Destroy(gameObject); return; }

        timer += Time.deltaTime;
        if (timer < spray.dripInterval) return;
        timer = 0f;

        float step = spray.dripStep;

        // Упёрлись в пол — стоп.
        if (Physics.Raycast(surfacePoint + outward * 0.08f, Vector3.down, step + 0.05f, solidMask, QueryTriggerInteraction.Ignore))
        {
            Destroy(gameObject);
            return;
        }

        // Ниже ещё есть стена?
        Vector3 next = surfacePoint + Vector3.down * step;
        if (!Physics.Raycast(next + outward * 0.3f, -outward, out RaycastHit wall, 0.6f, solidMask, QueryTriggerInteraction.Ignore))
        {
            Destroy(gameObject);
            return;
        }
        next = wall.point;

        steps++;
        float width = spray.dripWidth * Mathf.Lerp(1f, 0.5f, steps / (float)spray.dripMaxSteps); // сужается книзу
        streak.AddPoint(next + outward * Offset, outward, width);
        spray.CreateSmallLedge(next, outward, color, 0.3f);
        surfacePoint = next;

        if (steps >= spray.dripMaxSteps) Destroy(gameObject);
    }
}
