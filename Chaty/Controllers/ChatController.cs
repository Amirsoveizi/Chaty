using Chaty.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Chaty.Controllers
{
    public class ChatController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [Route("chat/delete/{username}")]
        public async Task<ActionResult> DeleteUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToAction("Index");
            }

            try
            {
                using (var db = new ChatContext())
                {
                    var userToDelete = await db.Users
                                               .Include(u => u.UserGroups) 
                                               .FirstOrDefaultAsync(u => u.UserName == username);

                    if (userToDelete == null)
                    {
                        return RedirectToAction("Index");
                    }

                    db.UserGroups.RemoveRange(userToDelete.UserGroups);

                    var messagesToDelete = await db.ChatMessages
                                                   .Where(m => m.SenderId == userToDelete.UserId || m.ReceiverId == userToDelete.UserId)
                                                   .ToListAsync();
                    db.ChatMessages.RemoveRange(messagesToDelete);

                    db.Users.Remove(userToDelete);

                    await db.SaveChangesAsync();

                    return RedirectToAction("Index");
                }
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index");
            }
        }
    }
}
