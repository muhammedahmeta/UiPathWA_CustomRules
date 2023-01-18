using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace UiPathWA_CustomRules
{
    public class MyCustomRules : IRegisterAnalyzerConfiguration
    {
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature("WorkflowAnalyzerV4"))    // Workflow Analyzer için versiyon doğrulaması yapılır
                return;
            //
            #region ActivityModel
            var ActivityRule = new Rule<IActivityModel>("LogMessageVariable", "M2A-ARM-001", CheckLogMessage);  // IActivityModel arayüzü ile aktivite bazlı Rule değişkeni tanımlanır. Parametre olarak ruleName, ruleId ve geriye InspectionResult tipinde veri dönen kontrol metodunu alır
            ActivityRule.DefaultErrorLevel = System.Diagnostics.TraceLevel.Warning; // Tanımlanan kuralın default uyarı seviyesi seçilir
            workflowAnalyzerConfigService.AddRule<IActivityModel>(ActivityRule);    // Kuralın Workflow Analyzer üzerine eklenme işlemi gerçekleştirilir
            #endregion
            //--------------------------------------------------------------------------------
            #region WorkflowModel
            var WorkflowRule = new Rule<IWorkflowModel>("ArgumentDirection", "M2A-WRM-001", CheckArgumentDirection);
            WorkflowRule.DefaultErrorLevel = System.Diagnostics.TraceLevel.Error;
            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(WorkflowRule);
            #endregion
            //--------------------------------------------------------------------------------
            #region ProjectModel
            var ProjectRule = new Rule<IProjectModel>("ProjectDirectory", "M2A-PRM-001", CheckProjectDirectory);
            ProjectRule.DefaultErrorLevel = System.Diagnostics.TraceLevel.Info;
            workflowAnalyzerConfigService.AddRule<IProjectModel>(ProjectRule);
            #endregion 
            //
            throw new NotImplementedException();
        }
        #region ActivityRuleMethod
        private InspectionResult CheckLogMessage(IActivityModel MyActivityModelToInspect, Rule ActivityRuleToInspect)
        {
            var ActivityType = MyActivityModelToInspect.Type.Split(",".ToCharArray()).FirstOrDefault().Split(".".ToCharArray()).LastOrDefault();    // IActivityModel arayüzünden gelen Type parametresi ile aktivitenin tipi belirlenir
            var IsVariableUsed = true;  // Kontrol sonrası ataması yapılacak değişken
            string MessageText = string.Empty;  // LogMessage aktivitesi içerisindeki Message argümanının tanımlı ifadenin atanacağı değişken
            var MessageList = new List<InspectionMessage>();    // Oluşacak hataların ekleneceği output listesi
            //
            if (ActivityType == "LogMessage")
            {
                MessageText = MyActivityModelToInspect.Arguments.Where(x => x.DisplayName.Equals("Message")).LastOrDefault().DefinedExpression; // LogMessage aktivitesindeki Message isimli argümanda tanımlı ifade ataması yapılır
                IsVariableUsed = System.Text.RegularExpressions.Regex.Replace(MessageText, "\"(.*?)\"", "").Split(Convert.ToChar("+")).Any(x => x.Trim().ToLower().Equals("workflowname")); // Tanınmlı mesaj ifadesindeki stringler replace edilir ve + işaretine göre split edilir. Bu işlem sonundaki listede workflowname değişkeni var mı tespit edilir
            }
            if (!IsVariableUsed)
            {
                MessageList.Add(new InspectionMessage() // Eğer değişken kullanılmadıyla output listesine InspectionMessage tipinde output mesajı eklenir
                {
                    Message = $"LogMessage activity not contains \"WorkflowName\" variable"
                });
            }
            //
            if (MessageList.Count > 0)
            {
                return new InspectionResult()   // Mesaj listesinin içi dolu ise InspectionResult tipinde hata parametresi true atanmış değişken geri gönderilir
                {
                    HasErrors = true,
                    InspectionMessages = MessageList,
                    RecommendationMessage = "Add variable \"WorkflowName\" to LogMessage",
                    ErrorLevel = ActivityRuleToInspect.ErrorLevel
                };
            }
            //
            return new InspectionResult { HasErrors = false }; // Herhangi bir hata yoksa InspectionResult tipinde hata parametresi false atanmış değişken geri gönderilir
        }
        #endregion
        //--------------------------------------------------------------------------------
        #region WorkflowRuleMethod
        private InspectionResult CheckArgumentDirection(IWorkflowModel MyWorkflowModelToInspect, Rule WorkflowRuleToInspect)
        {
            var IsExistWrongDirection = false;   // Kontrol sonrası ataması yapılacak değişken
            var MessageList = new List<InspectionMessage>();
            //
            foreach (var Argument in MyWorkflowModelToInspect.Arguments)
            {
                IsExistWrongDirection = false;
                //
                switch (Argument.Direction) // İlgili workflow içerisindeki herbir argümanın direction dipine göre isimlendirmesi prefix içeriyor mu kontrol edilir
                {
                    case ArgumentDirection.In:
                        if (!Argument.DisplayName.Substring(0, 3).Equals("in_"))
                            IsExistWrongDirection = true;
                        break;
                    case ArgumentDirection.Out:
                        if (!Argument.DisplayName.Substring(0, 4).Equals("out_"))
                            IsExistWrongDirection = true;
                        break;
                    case ArgumentDirection.InOut:
                        if (!Argument.DisplayName.Substring(0, 3).Equals("io_"))
                            IsExistWrongDirection = true;
                        break;
                    default:
                        IsExistWrongDirection = false;
                        break;
                }
                if (IsExistWrongDirection)
                {
                    MessageList.Add(new InspectionMessage()
                    {
                        Message = $"Argument: {Argument.DisplayName} in {MyWorkflowModelToInspect.DisplayName} workflow not contains a correct direction prefix"
                    });
                }
            }
            //
            if (MessageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    InspectionMessages = MessageList,
                    RecommendationMessage = "Add direction prefix to arguments",
                    ErrorLevel = WorkflowRuleToInspect.ErrorLevel
                };
            }
            //
            return new InspectionResult { HasErrors = false };
        }
        #endregion
        //--------------------------------------------------------------------------------
        #region ProjectRuleMethod
        private InspectionResult CheckProjectDirectory(IProjectModel MyProjectModelToInspect, Rule ProjectRuleToInspect)
        {
            var IsContainsInvalidChar = System.Text.RegularExpressions.Regex.IsMatch(MyProjectModelToInspect.Directory, "[ğüşöçıİĞÜŞÖÇ]");  // Projenin dosya dizini Türkçe karakter içeriyor mu kontrol edilir
            var MessageList = new List<InspectionMessage>();
            //
            if (IsContainsInvalidChar)
            {
                MessageList.Add(new InspectionMessage()
                {
                    Message = $"Project directory contains Turkish character"
                });
            }
            //
            if (MessageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    InspectionMessages = MessageList,
                    RecommendationMessage = "Check the project directory",
                    ErrorLevel = ProjectRuleToInspect.ErrorLevel
                };
            }
            //
            return new InspectionResult { HasErrors = false };
        }
            #endregion
    }
}
