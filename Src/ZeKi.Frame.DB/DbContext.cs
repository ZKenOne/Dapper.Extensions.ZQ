﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;
using Dapper;
using StackExchange.Profiling;
using ZeKi.Frame.Common;
using ZeKi.Frame.Model;

namespace ZeKi.Frame.DB
{
    /// <summary>
    /// 数据库上下文,同一请求共用一个实例
    /// </summary>
    public class DbContext : IDisposable
    {
        private static readonly string connStr = AppSettings.GetValue("ConnectionString");
        private static readonly DBEnums.DBType dbType = (DBEnums.DBType)AppSettings.GetValue<int>("DBType");
        private static readonly int commandTimeout = AppSettings.GetValue<int>("CommandTimeout");   //单位：秒

        private IDbConnection conn = null;
        private IDbTransaction tran = null;

        private IDbConnection Connection
        {
            get
            {
                //DbContext对象同一请求共用一个实例,所以这里不会出现并发实例化多个数据库连接
                if (conn == null)
                {
                    conn = GetConnection();
                }
                return conn;
            }
        }

        #region Insert
        /// <summary>
        /// 新增
        /// </summary>
        /// <param name="getId">是否获取当前插入的ID,不是自增则不需要关注此值</param>
        /// <returns>getId为true并且有自增列则返回插入的id值,否则为影响行数</returns>
        public int Insert<TModel>(TModel model, bool getId = false) where TModel : class
        {
            return Connection.Insert(model, getId, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 批量新增
        /// </summary>
        /// <param name="list"></param>
        /// <param name="ps">每批次数量,默认500</param>
        /// <returns>返回总影响行数</returns>
        public int BatchInsert<TModel>(IEnumerable<TModel> list, int ps = 500) where TModel : class
        {
            return Connection.BatchInsert(list, ps, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// bulkcopy,仅支持myssql数据库
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="connection"></param>
        /// <param name="entitysToInsert"></param>
        /// <param name="timeOut">超时时间,单位：秒</param>
        public void BulkCopyToInsert<TModel>(IEnumerable<TModel> entitysToInsert, SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.Default, int timeOut = 60 * 10) where TModel : class, new()
        {
            Connection.BulkCopyToInsert(entitysToInsert, copyOptions, tran, timeOut);
        }
        #endregion

        #region Update
        /// <summary>
        /// 修改(单个,根据主键)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public bool Update<TModel>(TModel model) where TModel : class
        {
            return Connection.Update(model, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 修改(根据自定义条件修改自定义值)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="setAndWhere">set和where的键值对,使用 匿名类、指定数据类型类<see cref="DataParameters"/>、字典(<see cref="Dictionary{TKey, TValue}"/>[键为string,值为object]、<see cref="Hashtable"/>)、自定义类
        /// <para>格式:new {renew_name="u",id=1},解析:set字段为renew_name="u",where条件为id=1</para>
        /// <para>修改值必须以renew_开头,如数据库字段名有此开头需要叠加</para>
        /// <para>如 where值中有集合/数组,则生成 in @Key ,sql: in ('','')</para>
        /// <para>set字段能否被修改受 <see cref="PropertyAttribute"/> 特性限制</para>
        /// </param>
        /// <returns></returns>
        public int Update<TModel>(object setAndWhere) where TModel : class
        {
            return Connection.Update<TModel>(setAndWhere, tran, commandTimeout: commandTimeout);
        }
        #endregion

        #region Delete
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="model">传递主键字段值即可,如需统一清除缓存,可以传递所有字段值数据</param>
        /// <returns></returns>
        public bool Delete<TModel>(TModel model) where TModel : class
        {
            return Connection.Delete(model, tran, commandTimeout: commandTimeout);
        }
        #endregion

        #region Query
        /// <summary>
        /// 查询列表
        /// </summary>
        /// <typeparam name="TModel">返回模型,如果与表不一致,需要在返回表添加表特性或者sql为全sql</typeparam>
        /// <param name="sql">可以书写where name=@name,会自动补全, 也可以书写 select * from tb where name=@name</param>
        /// <param name="param"></param>
        /// <returns>返回集合</returns>
        public IEnumerable<TModel> QueryList<TModel>(string sql, object param = null)
        {
            return Connection.QueryList<TModel>(sql, param, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="T">返回模型</typeparam>
        /// <param name="whereObj">使用 匿名类、指定数据类型类<see cref="DataParameters"/>、字典(<see cref="Dictionary{TKey, TValue}"/>[键为string,值为object]、<see cref="Hashtable"/>)、自定义类</param>
        /// <param name="orderStr">填写：id asc / id,name desc</param>
        /// <param name="selectFields">,分隔</param>
        /// <returns></returns>
        public IEnumerable<T> QueryList<T>(object whereObj = null, string orderStr = null, string selectFields = "*")
        {
            return Connection.QueryList<T>(whereObj, orderStr, selectFields, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="T">返回模型</typeparam>
        /// <param name="whereObj">使用 匿名类、指定数据类型类<see cref="DataParameters"/>、字典(<see cref="Dictionary{TKey, TValue}"/>[键为string,值为object]、<see cref="Hashtable"/>)、自定义类</param>
        /// <param name="orderStr">填写：id asc / id,name desc</param>
        /// <returns></returns>
        public IEnumerable<T> QueryList<T>(string sqlNoWhere, object whereObj = null, string orderStr = null)
        {
            return Connection.QueryList<T>(sqlNoWhere, whereObj, orderStr, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询单个
        /// </summary>
        /// <typeparam name="T">返回模型,如果与表不一致,需要在返回表添加表特性或者sql为全sql</typeparam>
        /// <param name="sql">可以书写where name=@name 也可以书写 select top 1 * from tb where name=@name</param>
        /// <param name="param"></param>
        /// <returns>返回单个</returns>
        public T QueryModel<T>(string sql, object param = null)
        {
            return Connection.QueryModel<T>(sql, param, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="T">返回模型</typeparam>
        /// <param name="whereObj">使用 匿名类、指定数据类型类<see cref="DataParameters"/>、字典(<see cref="Dictionary{TKey, TValue}"/>[键为string,值为object]、<see cref="Hashtable"/>)、自定义类</param>
        /// <param name="selectFields">,分隔</param>
        /// <param name="orderStr">填写：id asc / id,name desc</param>
        /// <returns></returns>
        public T QueryModel<T>(object whereObj, string orderStr = null, string selectFields = "*")
        {
            return Connection.QueryModel<T>(whereObj, orderStr, selectFields, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="T">返回模型</typeparam>
        /// <param name="whereObj">使用 匿名类、指定数据类型类<see cref="DataParameters"/>、字典(<see cref="Dictionary{TKey, TValue}"/>[键为string,值为object]、<see cref="Hashtable"/>)、自定义类</param>
        /// <param name="orderStr">填写：id asc / id,name desc</param>
        /// <returns></returns>
        public T QueryModel<T>(string sqlNoWhere, object whereObj = null, string orderStr = null)
        {
            return Connection.QueryModel<T>(sqlNoWhere, whereObj, orderStr, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询(可以对多个返回结果进行操作,最终返回一个)
        /// 使用参考: https://github.com/StackExchange/Dapper#multi-mapping
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="sql">需要输入全sql</param>
        /// <param name="map"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public IEnumerable<TReturn> QueryList<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map, object param = null)
        {
            return Connection.Query(sql, map, param, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 查询(可以对多个返回结果进行操作,最终返回一个)
        /// 使用参考: https://github.com/StackExchange/Dapper#multi-mapping
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="sql">需要输入全sql</param>
        /// <param name="map"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public IEnumerable<TReturn> QueryList<TFirst, TSecond, TThird, TReturn>(string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param = null)
        {
            return Connection.Query(sql, map, param, tran, commandTimeout: commandTimeout);
        }

        ///// <summary>
        ///// 查询返回多个结果集 (using之后在外面无法获取数据,可以在外部使用DbAction方法来完成)
        ///// </summary>
        ///// <param name="sql">需要输入全sql</param>
        ///// <param name="param"></param>
        ///// <returns></returns>
        //public GridReader QueryMultiple(string sql, object param = null)
        //{
        //    using (var _conn = GetConnection())
        //    {
        //        return DBConnection.QueryMultiple(sql, param, commandTimeout: _commandTimeout);
        //    }
        //}

        /// <summary>
        /// 分页查询返回集合
        /// </summary>
        /// <typeparam name="T">返回模型</typeparam>
        /// <param name="pcp">分页模型(参数传递查看PageParameters字段注解)</param>
        /// <param name="param">同dapper参数传值</param>
        /// <returns></returns>
        public PageData<T> PageList<T>(PageParameters pcp, object param = null) where T : class, new()
        {
            return Connection.PageList<T>(pcp, param, tran, commandTimeout: commandTimeout);
        }

        ///// <summary>
        ///// 分页查询返回数据集
        ///// </summary>
        ///// <param name="pcp">分页必填参数</param>
        ///// <param name="param">该参数需传原生参数化对象.如mssql:SqlParameter,多个用List包含传递</param>
        ///// <returns></returns>
        //public DataSet PageDataSet(PageParameters pcp, object param = null)
        //{
        //    using (var _conn = GetConnection())
        //    {
        //        return DBConnection.PageDataSet(pcp, param);
        //    }
        //}

        ///// <summary>
        ///// 分页查询返回数据表
        ///// </summary>
        ///// <param name="pcp">分页必填参数</param>
        ///// <param name="param">该参数需传原生参数化对象.如mssql:SqlParameter,多个用List包含传递</param>
        ///// <returns></returns>
        //public DataTable PageDataTable(PageParameters pcp, object param = null)
        //{
        //    using (var _conn = GetConnection())
        //    {
        //        return DBConnection.PageDataTable(pcp, param);
        //    }
        //}
        #endregion

        #region Procedure
        /// <summary>
        /// 执行查询存储过程(用于查询数据)
        /// <para>参数传递参考：https://github.com/StackExchange/Dapper#stored-procedures </para>
        /// </summary>
        /// <typeparam name="T">返回模型,如果没有对应模型类接收,可以传入dynamic,然后序列化再反序列化成List泛型参数:Hashtable</typeparam>
        /// <param name="proceName">存储过程名</param>
        /// <param name="param">特定键值字典/Hashtable/匿名类/自定义类,过程中有OutPut或者Return参数,使用<see cref="DataParameters"/></param>
        /// <returns>返回集合</returns>
        public IEnumerable<T> QueryProcedure<T>(string proceName, object param = null)
        {
            return Connection.QueryProcedure<T>(proceName, param, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// 执行存储过程(查询数据使用QueryProcedure方法)
        /// <para>参数传递参考：https://github.com/StackExchange/Dapper#stored-procedures </para>
        /// </summary>
        /// <param name="proceName">存储过程名</param>
        /// <param name="param">特定键值字典/Hashtable/匿名类/自定义类,过程中有OutPut或者Return参数,使用<see cref="DataParameters"/></param>
        /// <param name="transaction"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        public void ExecProcedure(string proceName, object param = null)
        {
            Connection.ExecProcedure(proceName, param, tran, commandTimeout: commandTimeout);
        }
        #endregion

        #region Statistics
        /// <summary>
        /// Count统计
        /// </summary>
        /// <param name="sqlWhere">1=1,省略where</param>
        /// <param name="param"></param>
        /// <typeparam name="T">模型对象(获取表名)</typeparam>
        /// <returns></returns>
        public int Count<TModel>(string sqlWhere, object param = null) where TModel : class
        {
            return Connection.Count<TModel>(sqlWhere, param, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Count统计
        /// </summary>
        /// <param name="whereObj">使用 匿名类、指定数据类型类<see cref="DataParameters"/>、字典(<see cref="Dictionary{TKey, TValue}"/>[键为string,值为object]、<see cref="Hashtable"/>)、自定义类</param>
        /// <returns></returns>
        public int Count<TModel>(object whereObj = null) where TModel : class
        {
            return Connection.Count<TModel>(whereObj, tran, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Sum统计
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="field">需要sum的字段</param>
        /// <param name="sqlWhere">1=1,省略where</param>
        /// <param name="param"></param>
        /// <returns></returns>
        public TResult Sum<TModel, TResult>(string field, string sqlWhere, object param = null) where TModel : class
        {
            return Connection.Sum<TModel, TResult>(field, sqlWhere, param, tran, commandTimeout: commandTimeout);
        }
        #endregion

        #region Excute
        /// <summary>
        /// 执行sql(非查询)
        /// </summary>
        /// <returns></returns>
        public int Execute(string sql, object param = null)
        {
            return Connection.Execute(sql, param, tran, commandTimeout: commandTimeout);
        }
        #endregion

        #region Dapper原生
        ///// <summary>
        ///// 执行数据库操作,会自动释放,操作QueryMultiple可以用这个
        ///// </summary>
        ///// <param name="action"></param>
        //public void DbAction(Action<IDbConnection> action)
        //{
        //    action(conn);
        //}
        #endregion

        #region Transaction
        /// <summary>
        /// 批量数据事务提交
        /// </summary>
        /// <param name="strSqls">T-SQL语句</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        public int ExecTransaction(string strSqls, object param = null)
        {
            return Connection.ExecTransaction(strSqls, param);
        }

        public void BeginTransaction(IsolationLevel isolation = IsolationLevel.ReadCommitted)
        {
            tran = (Connection as DbConnection).BeginTransaction(isolation);
        }

        public void CommitTransaction()
        {
            if (tran != null)
            {
                tran.Commit();
                tran = null;
            }
        }

        public void RollbackTransaction()
        {
            if (tran != null)
            {
                tran.Rollback();
                tran = null;
            }
        }
        #endregion

        #region DataTable 
        ///// <summary>
        ///// 获取DataTable(与dapper无关)
        ///// </summary>
        ///// <param name="commandText"></param>
        ///// <param name="param">该参数需传原生参数化对象.如mssql:SqlParameter,多个用List包含传递</param>
        ///// <param name="commandType">CommandType为StoredProcedure时,直接写存储过程名</param>
        ///// <returns></returns>
        //public DataTable ExecDataTable(string commandText, object param = null, CommandType commandType = CommandType.Text)
        //{
        //   return DBConnection.ExecDataTable(commandText, param, commandType);
        //}
        #endregion

        #region DataSet
        ///// <summary>
        ///// 获取DataSet(与dapper无关)
        ///// </summary>
        ///// <param name="commandText"></param>
        ///// <param name="param">该参数需传原生参数化对象.如mssql:SqlParameter,多个用List包含传递</param>
        ///// <param name="commandType">CommandType为StoredProcedure时,直接写存储过程名</param>
        ///// <returns></returns>
        //public DataSet ExecDataSet(string commandText, object param = null, CommandType commandType = CommandType.Text)
        //{
        //   return DBConnection.ExecDataSet(commandText, param, commandType);
        //}
        #endregion

        #region 内部帮助/受保护类型 方法
        /// <summary>
        /// 获取连接对象
        /// </summary>
        /// <returns></returns>
        private IDbConnection GetConnection()
        {
            IDbConnection _conn = null;
            switch (dbType)
            {
                case DBEnums.DBType.MSSQL:
                    _conn = new SqlConnection(connStr);
                    break;
                case DBEnums.DBType.MYSQL:

                    break;
            }
            if (_conn == null)
                throw new NotImplementedException($"未实现该数据库");
            //core在此类的构造函数中获取不到MiniProfiler.Current其值
            //因为还没执行ProfilingFilterAttribute.OnActionExecutionAsync方法的StartNew,所以为null
            if (MiniProfiler.Current != null) //MiniProfiler初始化
                _conn = new StackExchange.Profiling.Data.ProfiledDbConnection((DbConnection)_conn, MiniProfiler.Current);
            if (_conn.State == ConnectionState.Closed)
                _conn.Open();
            return _conn;
        }

        #endregion

        #region 释放链接(请求完后容器会调用)
        public void Dispose()
        {
            if (conn != null)
                conn.Dispose();
            if (tran != null)
                tran.Dispose();
        }
        #endregion
    }
}
