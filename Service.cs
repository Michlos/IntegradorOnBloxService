using Integrador.Domain.Cliente;
using Integrador.Domain.Email;
using Integrador.Domain.EmailConfigure;
using Integrador.Domain.OnBloxConfigure;
using Integrador.Services.Cliente;
using Integrador.Services.Email;
using Integrador.Services.EmailConfigure;
using Integrador.Services.OnBloxConfigure;
using Integrador.WebService;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace IntegradorOnBloxService
{

    public partial class Service : ServiceBase
    {
        //TODO: SETAR O TIMER PARA EXECUTAR O SERVIÇO

        //SERVIÇOS DE BUSCA DE DADOS
        private OnBloxService _onBloxService;
        private EmailConfigureService _emailConfigureService;
        private EmailService _emailService;
        private ClienteService _clienteService;
        private JsonService _jsonService;

        //MODELS DE CONFIGURAÇÃO 
        private OnBloxConfigureModel OnBloxConfigureModel = new OnBloxConfigureModel();
        private EmailConfigureModel EmailConfigureModel = new EmailConfigureModel();

        //MODELS DE ENTIDADES
        private List<EmailModel> EmailModelList;
        private ClienteModel ClienteModel = new ClienteModel();
        private List<ClienteModel> ClienteModelLilst;

        public Service()
        {
            InitializeComponent(); 
        }

        protected override void OnStart(string[] args)
        {
            BuscaConfiguracoes();
            ReceberEmails();
            SalvarClientes(); 
            IntegrararClientes();

        }

        protected override void OnStop()
        {
        }


        public void BuscaConfiguracoes()
        {
            this.OnBloxConfigureModel = _onBloxService.GetOnBloxConfigure();
            this.EmailConfigureModel = _emailConfigureService.GetEmailConfigure();
        }

        public void ReceberEmails()
        {
            EmailModelList = new List<EmailModel>();
            _emailService.ConnectHost(true);
            EmailModelList = _emailService.ReceberMensagens(
                this.EmailConfigureModel.CaixaDeEmail,
                this.EmailConfigureModel.AssuntoEmail);
            _emailService.SalvarEmailsNoBancoDeDados(this.EmailModelList);

        }

        public void SalvarClientes()
        {
            _emailService.SalvarClienteNoBanco();
        }

        public void IntegrararClientes()
        {
            this.ClienteModelLilst = new List<ClienteModel>();
            var clienteList = _clienteService.GetAll();
            foreach (var item in clienteList)
            {
                this.ClienteModelLilst.Add(item as ClienteModel);
            }

            foreach (var item in this.ClienteModelLilst)
            {
                if (item != null)
                {
                    _jsonService.SendData(item);
                    _clienteService.SetIntegrado(item);                       
                }
            }


        }
    }
}
