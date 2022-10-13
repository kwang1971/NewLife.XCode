﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using NewLife;
using NewLife.Log;
using XCode.Code;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>魔方页面生成器</summary>
public class CubeBuilder : ClassBuilder
{
    #region 属性
    /// <summary>区域模版</summary>
    public String AreaTemplate { get; set; } = @"using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace {Project}.Areas.{Name}
{
    [DisplayName(""{DisplayName}"")]
    public class {Name}Area : AreaBase
    {
        public {Name}Area() : base(nameof({Name}Area).TrimEnd(""Area"")) { }
    }
}";

    /// <summary>控制器模版</summary>
    public String ControllerTemplate { get; set; } = @"using {Namespace};
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace GpsWeb.Areas.Gps.Controllers
{
    [Menu(30, true)]
    [GpsArea]
    public class TirePressureController : ReadOnlyEntityController<TirePressure>
    {
        static TirePressureController()
        {
            ListFields.RemoveField(""Id"", ""Creator"");

            {
                var df = ListFields.GetField(""TraceId"") as ListField;
                df.DisplayName = ""跟踪"";
                df.Url = StarHelper.BuildUrl(""{TraceId}"");
                df.DataVisible = e => !(e as TirePressure).TraceId.IsNullOrEmpty();
            }
        }

        protected override IEnumerable<TirePressure> Search(Pager p)
        {
            var deviceId = p[""deviceId""].ToInt(-1);
            var provider = p[""provider""];

            var start = p[""dtStart""].ToDateTime();
            var end = p[""dtEnd""].ToDateTime();

            return TirePressure.Search(deviceId, provider, start, end, p[""Q""], p);
        }
    }
}";
    #endregion

    #region 静态
    /// <summary>生成魔方区域</summary>
    /// <param name="option">可选项</param>
    /// <returns></returns>
    public static Int32 BuildArea(BuilderOption option)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        var file = $"{option.ConnName}Area.cs";
        file = option.Output.CombinePath(file);
        file = file.GetBasePath();

        // 文件已存在，不要覆盖
        if (File.Exists(file)) return 0;

        if (Debug) XTrace.WriteLine("生成魔方区域 {0}", file);

        var builder = new CubeBuilder();
        var code = builder.AreaTemplate;

        //code = code.Replace("{Namespace}", option.Namespace);
        code = code.Replace("{Project}", option.ConnName + "Web");
        code = code.Replace("{Name}", option.ConnName);
        code = code.Replace("{DisplayName}", option.DisplayName);

        // 输出到文件
        file.EnsureDirectory(true);
        File.WriteAllText(file, code);

        return 1;
    }

    /// <summary>生成控制器</summary>
    /// <param name="tables">表集合</param>
    /// <param name="option">可选项</param>
    /// <returns></returns>
    public static Int32 BuildController(IList<IDataTable> tables, BuilderOption option = null)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        var file = option.ConnName;
        if (file.IsNullOrEmpty()) file = "Model";
        file += ".htm";
        file = file.GetBasePath();

        if (Debug) XTrace.WriteLine("生成控制器 {0}", file);

        var count = 0;
        var writer = new StringWriter();

        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new HtmlBuilder
            {
                Writer = writer,
                Table = item,
                Option = option.Clone(),
            };
            if (Debug) builder.Log = XTrace.Log;

            builder.Load(item);

            // 执行生成
            builder.Execute();
            //builder.Save(null, true, false);

            count++;
        }

        // 输出到文件
        File.WriteAllText(file, writer.ToString());

        return count;
    }
    #endregion

    #region 方法
    /// <summary>生成前</summary>
    protected override void OnExecuting()
    {
        if (Table.DisplayName.IsNullOrEmpty())
            WriteLine("<h3>{0}</h3>", Table.TableName);
        else
            WriteLine("<h3>{0}（{1}）</h3>", Table.DisplayName, Table.TableName);

        WriteLine("<table>");
        {
            WriteLine("<thead>");
            WriteLine("<tr>");
            WriteLine("<th>名称</th>");
            WriteLine("<th>显示名</th>");
            WriteLine("<th>类型</th>");
            WriteLine("<th>长度</th>");
            WriteLine("<th>精度</th>");
            WriteLine("<th>主键</th>");
            WriteLine("<th>允许空</th>");
            WriteLine("<th>备注</th>");
            WriteLine("</tr>");
            WriteLine("</thead>");
        }
        WriteLine("<tbody>");
    }

    /// <summary>生成后</summary>
    protected override void OnExecuted()
    {
        WriteLine("</tbody>");
        WriteLine("</table>");

        WriteLine("<br></br>");
    }

    /// <summary>生成主体</summary>
    protected override void BuildItems()
    {
        for (var i = 0; i < Table.Columns.Count; i++)
        {
            var column = Table.Columns[i];

            // 跳过排除项
            if (!ValidColumn(column)) continue;

            if (i > 0) WriteLine();
            BuildItem(column);
        }
    }

    /// <summary>生成项</summary>
    /// <param name="column"></param>
    protected override void BuildItem(IDataColumn column)
    {
        WriteLine("<tr>");
        {
            WriteLine("<td>{0}</td>", column.ColumnName);
            WriteLine("<td>{0}</td>", column.DisplayName);
            WriteLine("<td>{0}</td>", column.RawType ?? column.DataType?.FullName.TrimStart("System."));

            if (column.Length > 0)
                WriteLine("<td>{0}</td>", column.Length);
            else
                WriteLine("<td></td>");

            if (column.Precision > 0 || column.Scale > 0)
                WriteLine("<td>({0}, {1})</td>", column.Precision, column.Scale);
            else
                WriteLine("<td></td>");

            if (column.Identity)
                WriteLine("<td title=\"自增\">AI</td>");
            else if (column.PrimaryKey)
                WriteLine("<td title=\"主键\">PK</td>");
            else if (Table.Indexes.Any(e => e.Unique && e.Columns.Length == 1 && e.Columns[0].EqualIgnoreCase(column.Name, column.ColumnName)))
                WriteLine("<td title=\"唯一索引\">UQ</td>");
            else
                WriteLine("<td></td>");

            WriteLine("<td>{0}</td>", column.Nullable ? "" : "N");
            WriteLine("<td>{0}</td>", column.Description?.TrimStart(column.DisplayName).TrimStart("。", "，"));
        }
        WriteLine("</tr>");
    }
    #endregion

    #region 辅助
    /// <summary>写入</summary>
    /// <param name="value"></param>
    protected override void WriteLine(String value = null)
    {
        if (!value.IsNullOrEmpty() && value.Length > 2 && value[0] == '<' && value[1] == '/') SetIndent(false);

        base.WriteLine(value);

        if (!value.IsNullOrEmpty() && value.Length > 2 && value[0] == '<' && value[1] != '/' && !value.Contains("</")) SetIndent(true);
    }
    #endregion
}