using UnityEngine;

// Подтёк краски на стене. Создаётся баллончиком, когда игрок держит его на одном месте стены.
// Каждые dripInterval секунд стекает на dripStep вниз: тянет по стене тонкую выпуклую струйку
// (сужается книзу). Когда останавливается — внизу остаётся капля, на которую можно встать.
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
    bool finished;

    const float Offset = 0.035f; // чуть над основным граффити

    public void Init(PaintSpray spray, Vector3 surfacePoint, Vector3 outward, Color color)
    {
        this.spray = spray;
        this.surfacePoint = surfacePoint;
        this.outward = outward;
        this.color = color;
        solidMask = ~((1 << PaintSpray.PlayerLayer) | (1 << PaintSpray.PaintLayer));

        streak = spray.CreateDripStreak(color);
        streak.AddPoint(surfacePoint + outward * Offset, outward, spray.dripWidth);
    }

    void Update()
    {
        if (spray == null) { Destroy(gameObject); return; }
        if (finished) return;

        timer += Time.deltaTime;
        if (timer < spray.dripInterval) return;
        timer = 0f;

        float step = spray.dripStep;

        // Упёрлись в пол — стоп.
        if (Physics.Raycast(surfacePoint + outward * 0.08f, Vector3.down, step + 0.05f, solidMask, QueryTriggerInteraction.Ignore))
        {
            Finish();
            return;
        }

        // Ниже ещё есть стена?
        Vector3 next = surfacePoint + Vector3.down * step;
        if (!Physics.Raycast(next + outward * 0.3f, -outward, out RaycastHit wall, 0.6f, solidMask, QueryTriggerInteraction.Ignore))
        {
            Finish();
            return;
        }
        next = wall.point;

        steps++;
        float width = spray.dripWidth * Mathf.Lerp(1f, 0.5f, steps / (float)spray.dripMaxSteps); // сужается книзу
        streak.AddPoint(next + outward * Offset, outward, width);
        surfacePoint = next;

        if (steps >= spray.dripMaxSteps) Finish();
    }

    void Finish()
    {
        finished = true;
        if (steps > 0) spray.CreateDrop(surfacePoint, outward, color);
        Destroy(gameObject);
    }
}
