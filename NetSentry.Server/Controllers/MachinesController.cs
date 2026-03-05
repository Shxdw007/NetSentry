using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;
using NetSentry.Server.Models;
using NetSentry.Server.Models.Responses;

namespace NetSentry.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Защита: доступ только с JWT токеном
    public class MachinesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MachinesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/machines
        [HttpGet]
        public async Task<IActionResult> GetAllMachines()
        {
            var machines = await _context.Machines.ToListAsync();
            return Ok(ApiResponse<List<Machine>>.Ok(machines));
        }

        // GET: api/machines/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachineById(int id)
        {
            var machine = await _context.Machines.FindAsync(id);

            if (machine == null)
                return NotFound(ApiResponse<Machine>.Error("Машина не найдена"));

            return Ok(ApiResponse<Machine>.Ok(machine));
        }

        // POST: api/machines
        [HttpPost]
        [Authorize(Roles = "Admin")] // Только админы могут вручную добавлять машины через API
        public async Task<IActionResult> CreateMachine([FromBody] Machine machine)
        {
            machine.FirstConnected = DateTime.UtcNow;
            machine.LastConnected = DateTime.UtcNow;
            machine.Status = "Offline"; // По умолчанию

            _context.Machines.Add(machine);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<Machine>.Ok(machine, "Машина успешно добавлена"));
        }

        // PUT: api/machines/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMachine(int id, [FromBody] Machine updatedMachine)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
                return NotFound(ApiResponse<Machine>.Error("Машина не найдена"));

            machine.Name = updatedMachine.Name;
            machine.OsVersion = updatedMachine.OsVersion;
            machine.CpuName = updatedMachine.CpuName;
            machine.GpuName = updatedMachine.GpuName;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<Machine>.Ok(machine, "Данные машины обновлены"));
        }

        // DELETE: api/machines/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Удаление строго для админов
        public async Task<IActionResult> DeleteMachine(int id)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
                return NotFound(ApiResponse<Machine>.Error("Машина не найдена"));

            _context.Machines.Remove(machine);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<bool>.Ok(true, "Машина успешно удалена"));
        }
    }
}