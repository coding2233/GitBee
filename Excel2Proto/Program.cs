// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");

using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        new ExcelToProtobuf("Excel/Test.xlsx");
    }
}

public class ExcelToProtobuf
{

    public ExcelToProtobuf(string excelPath)
    {

        using (var stream = new FileStream(excelPath, FileMode.Open))
        {
            stream.Position = 0;
            XSSFWorkbook xssWorkbook = new XSSFWorkbook(stream);
            //for (int i = 0; i < xssWorkbook.NumberOfSheets; i++)
            //{
            //    var sheet = xssWorkbook.GetSheetAt(i);
            //    ReadSheet(sheet);
            //}
            var sheet = xssWorkbook.GetSheetAt(0);
            var msgTemplates = ReadSheet(sheet);
            var proto  = MessageToProto();
        }
    }

    public string MessageToProto()
    {
        //if (m_messageTemplates != null)
        //{
            
        //}
        return "";
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
        MessageTemplate msgTempalye = new MessageTemplate(msgName);
        //.proto
        var typeRow = sheet.GetRow(rowIndex + 1);
        var varRow = sheet.GetRow(rowIndex + 2);
        for (int j = 1; j < typeRow.LastCellNum; j++)
        {
            msgTempalye.Types.Add(typeRow.GetCell(j).StringCellValue);
            msgTempalye.Vars.Add(varRow.GetCell(j).StringCellValue);
        }

        //data
        for (int i = rowIndex + 3; i < sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);

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
    public List<string> Types;
    public List<string> Vars;

    public MessageTemplate(string name)
    {
        Name = name;
        Types = new List<string>();
        Vars = new List<string>();
    }
}