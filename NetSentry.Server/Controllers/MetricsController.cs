using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;
using NetSentry.Server.Models;
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
        // POST: api/metrics
        // Этот метод будет принимать данные от C#-агентов и JMeter
        [HttpPost]
        [AllowAnonymous] // Разрешаем агентам слать данные без авторизации для тестов
        public async Task<IActionResult> AddMetric([FromBody] MetricDto request)
        {
            // Убедись, что свойства (CpuTemp и т.д.) совпадают с твоей моделью Metric
            var newMetric = new Metric
            {
                MachineId = request.MachineId, 
                Timestamp = DateTime.UtcNow,
                CpuTemp = request.CpuTemp,
                GpuTemp = request.GpuTemp
            };

            _context.Metrics.Add(newMetric);
            await _context.SaveChangesAsync();

            // Возвращаем статус 201 (Создано) - именно его ждет JMeter в нашем тест-кейсе
            return StatusCode(201, newMetric);
        }

        // Класс-шаблон для приема JSON
        public class MetricDto
        {
            public int MachineId { get; set; }
            public float CpuTemp { get; set; } // Поменяли double на float
            public float GpuTemp { get; set; } // Поменяли double на float
        }
    }
}