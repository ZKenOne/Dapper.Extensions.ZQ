﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZeKi.Frame.Model;

namespace ZeKi.Frame.IDAL
{
    public interface ISysUserInfoDAL : IBaseDAL
    {
        string GetUserNameById(int id);
    }
}
