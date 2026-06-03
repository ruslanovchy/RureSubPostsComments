using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace RureSubPostsComments.Controllers;

public class StatusController : Controller
{
    public IActionResult Index()
    {
        return Ok("Posts Comments service is working!");
    }
}
