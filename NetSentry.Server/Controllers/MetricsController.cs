using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;
using System.Linq;
using System.Threading.Tasks;

namespace NetSentry.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MetricsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MetricsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/metrics/history
        [HttpGet("history")]
        [AllowAnonymous] // Разрешаем доступ без JWT токена, чтобы не усложнять Python-скрипт
        public async Task<IActionResult> GetMetricsHistory()
        {
            // Берем последние 100 записей из базы
            var metrics = await _context.Metrics
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .Select(m => new
                {
                    m.Timestamp,
                    m.CpuTemp,
                    m.GpuTemp
                })
                .ToListAsync();

            // Для красивого графика нам нужен порядок времени "слева направо" (от старых к новым),
            // поэтому переворачиваем список перед отправкой
            metrics.Reverse();

            return Ok(metrics); // Отдаем чистый JSON
        }
        
        // GET: api/metrics/current
        // Этот метод вернет список всех ПК и их САМЫЕ СВЕЖИЕ показатели
        [HttpGet("current")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCurrentStatus()
        {
            var machines = await _context.Machines
                .Select(m => new
                {
                    m.Name,
                    m.Status,
                    m.CpuName,
                    m.GpuName,
                    // Берем только одну, самую последнюю метрику для каждого ПК
                    LatestMetric = m.Metrics.OrderByDescending(x => x.Timestamp).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(machines);
        }
    }       
}