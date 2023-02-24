// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");

using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        new ExcelToProtobuf("Excel/Test.xlsx");
    }
}

public class ExcelToProtobuf
{
    string m_protoOutPath;
    string m_protoVersion = "syntax = \"proto3\";";
    StringBuilder m_stringBuilder;
    public ExcelToProtobuf(string excelPath)
    {
        m_protoOutPath = Path.GetDirectoryName(excelPath);

        using (var stream = new FileStream(excelPath, FileMode.Open))
        {
            stream.Position = 0;
            XSSFWorkbook xssWorkbook = new XSSFWorkbook(stream);
            for (int i = 0; i < xssWorkbook.NumberOfSheets; i++)
            {
                var sheet = xssWorkbook.GetSheetAt(i);
                var msgTemplates = ReadSheet(sheet);
                if (msgTemplates.Count > 0)
                {
                    string protoPath = Path.Combine(m_protoOutPath, sheet.SheetName + ".proto");

                    MessageToProto(msgTemplates, protoPath);
                }
            }
        }
    }

    public void MessageToProto(List<MessageTemplate> messages, string protoPath)
    {
        if (m_stringBuilder == null)
        {
            m_stringBuilder = new StringBuilder();
        }
        else
        {
            m_stringBuilder.Clear();
        }

        m_stringBuilder.AppendLine(m_protoVersion);
        m_stringBuilder.AppendLine("");
        foreach (var itemMessage in messages)
        {
            m_stringBuilder.Append("message ");
            m_stringBuilder.AppendLine(itemMessage.Name);
            m_stringBuilder.AppendLine("{");
            for (int i = 0; i < itemMessage.Types.Count; i++)
            {
                m_stringBuilder.Append("\t//");
                m_stringBuilder.AppendLine(itemMessage.Desc[i]);
                m_stringBuilder.Append("\t");
                m_stringBuilder.Append(itemMessage.Types[i]);
                m_stringBuilder.Append(" ");
                m_stringBuilder.Append(itemMessage.Vars[i]);
                m_stringBuilder.Append(" = ");
                m_stringBuilder.Append(i+1);
                m_stringBuilder.AppendLine(";");
            }
            m_stringBuilder.AppendLine("}\n");
        }

        File.WriteAllText(protoPath, m_stringBuilder.ToString());
    }

    private List<MessageTemplate> ReadSheet(ISheet sheet, int rowIndex = 0)
    {
        List<MessageTemplate> messageTemplates = new List<MessageTemplate>();

        for (int i = rowIndex; i < sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null)
            {
                continue;
            }

            var cell = row.GetCell(0);
            if (cell != null)
            {
                if ("#message".Equals(cell.StringCellValue))
                {
                    var msgTemplate = ParseMessage(sheet, i, 0);
                    messageTemplates.Add(msgTemplate);
                    i += 2;
                }
            }
        }

        return messageTemplates;
    }

    private MessageTemplate ParseMessage(ISheet sheet, int rowIndex, int cellIndex)
    {
        string msgName = sheet.GetRow(rowIndex).GetCell(cellIndex + 1).StringCellValue;
        string msgId = sheet.GetRow(rowIndex).GetCell(cellIndex + 2).StringCellValue;
        MessageTemplate msgTempalye = new MessageTemplate(msgName, msgId);
        //.proto
        var typeRow = sheet.GetRow(rowIndex + 1);
        var varRow = sheet.GetRow(rowIndex + 2);
        var descRow = sheet.GetRow(rowIndex + 3);
        for (int j = 1; j < typeRow.LastCellNum; j++)
        {
            string typeCellValue = typeRow.GetCell(j).StringCellValue;
            string varCellValue = varRow.GetCell(j).StringCellValue;
            msgTempalye.Types.Add(typeCellValue);
            msgTempalye.Vars.Add(varCellValue);
            msgTempalye.Desc.Add(descRow.GetCell(j).StringCellValue);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(msgTempalye.Vars[j]);
            stringBuilder.Append(":");
            if (typeCellValue.Contains("repeated"))
            {
                stringBuilder.Append("[");
            }
            msgTempalye.Data.Add(stringBuilder);
        }

        //data
        for (int i = rowIndex + 4; i < sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            //if (row == null)
            //{
            //    if (typeCellValue.Contains("repeated"))
            //    {
            //        stringBuilder.AppendLine("]");
            //    }
            //}
            for (int j = 1; j < row.LastCellNum; j++)
            {
                string typeCellValue = msgTempalye.Types[j-1];
                string varCellValue = msgTempalye.Vars[j - 1];
                var stringBuilder = msgTempalye.Data[j - 1];

            }


           

            ////再遍历
            //if (row == null)
            //{
            //    ReadSheet(sheet, i+1);
            //}
        }

        return msgTempalye;
    }

}


public class MessageTemplate
{
    public string Name;
    public string Id;
    public List<string> Types;
    public List<string> Vars;
    public List<string> Desc;
    public List<StringBuilder> Data;
    public MessageTemplate(string name, string id)
    {
        Name = name;
        Id = id;
        Types = new List<string>();
        Vars = new List<string>();
        Desc = new List<string>();
        Data = new List<StringBuilder>();
    }
}