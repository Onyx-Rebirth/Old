using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnyxSAT.Data;
using OnyxSAT.Models;

namespace OnyxSAT.Controllers
{
  [Produces("application/json")]
  [Route("api/Classes")]
  public class ClassesController : Controller
  {
    private readonly ApplicationDbContext _context;

    public ClassesController(ApplicationDbContext context)
    {
      _context = context;
    }

    // GET: api/Classes
    [HttpGet]
    public IEnumerable<Class> GetClasses()
    {
      return _context.Classes;
    }

    // GET: api/Classes/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetClass([FromRoute] int id)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      var @class = await _context.Classes
        .Include(c => c.Sessions)
        .ThenInclude(s => s.Attendances)
        .Include(c => c.Enrolments)
        .ThenInclude(e => e.User)
        .ThenInclude(u => u.Cards)
        .AsNoTracking()
        .SingleOrDefaultAsync(m => m.ClassId == id);

      if (@class == null)
      {
        return NotFound();
      }
      return Ok(@class);
    }

    // PUT: api/Classes/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutClass([FromRoute] int id, [FromBody] Class @class)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      if (id != @class.ClassId)
      {
        return BadRequest();
      }

      _context.Entry(@class).State = EntityState.Modified;

      try
      {
        await _context.SaveChangesAsync();
      }
      catch (DbUpdateConcurrencyException)
      {
        if (!ClassExists(id))
        {
          return NotFound();
        }
        else
        {
          throw;
        }
      }

      return NoContent();
    }

    // POST: api/Classes
    [HttpPost]
    public async Task<IActionResult> PostClass([FromBody] Class @class)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      _context.Classes.Add(@class);
      await _context.SaveChangesAsync();
      await CreateSessions(@class);

      return CreatedAtAction("GetClass", new { id = @class.ClassId }, @class);
    }

    // POST: api/Classes/3/enrol
    [HttpPost("{id}/enrol")]
    public async Task<IActionResult> EnrolStudent([FromRoute] int id, [FromBody] Enrolment @enrolment)
    {
      @enrolment.ClassId = id;

      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      _context.Enrolments.Add(@enrolment);

      try
      {
        await _context.SaveChangesAsync();
      }
      catch (DbUpdateException)
      {
        var studentIsEnrolled = _context.Enrolments.Any(e => e.ClassId == @enrolment.ClassId && e.UserId == @enrolment.UserId);

        if (studentIsEnrolled)
        {
          return new StatusCodeResult(StatusCodes.Status409Conflict);
        }
        else
        {
          throw;
        }
      }

      return CreatedAtAction("EnrolStudent", new { classId = @enrolment.ClassId, userId = @enrolment.UserId }, @enrolment);
    }

    // DELETE: api/Classes/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteClass([FromRoute] int id)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      var @class = await _context.Classes.SingleOrDefaultAsync(m => m.ClassId == id);
      if (@class == null)
      {
        return NotFound();
      }

      _context.Classes.Remove(@class);
      await _context.SaveChangesAsync();

      return Ok(@class);
    }
    
    private async Task CreateSessions(Class @class)
    {
      var session = new Session { ClassId = @class.ClassId, RoomNumber = @class.Location };

      for (DateTime dt = @class.StartTime; dt < @class.EndTime; dt = dt.AddDays(1.0))
      {
        if (dt.DayOfWeek.ToString() == @class.DayOfWeek)
        {
          session.DateTime = dt;
          _context.Sessions.Add(session);
          _context.SaveChangesAsync();
          if (dt.DayOfWeek.ToString() == @class.DayOfWeek)
          {
            session.DateTime = dt;
            _context.Sessions.Add(session);
            try
            {
              _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
              throw;
            }
          }
        }
      }
    }
    private bool ClassExists(int id)
    {
      return _context.Classes.Any(e => e.ClassId == id);
    }
    private bool SessionExists(DateTime id)
    {
      return _context.Sessions.Any(e => e.DateTime == id);
    }
  }
}