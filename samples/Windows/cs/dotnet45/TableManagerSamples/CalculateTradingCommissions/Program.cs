using System;
using System.Collections.Specialized;
using System.Text;
using System.Configuration;
using System.Threading;
using fxcore2;

namespace CalculateTradingCommissions
{
    class Program
    {
        static SessionStatusListener statusListener = null;

        static void Main(string[] args)
        {
            O2GSession session = null;

            try
            {
                LoginParams loginParams = new LoginParams(ConfigurationManager.AppSettings);
                SampleParams sampleParams = new SampleParams(ConfigurationManager.AppSettings);

                PrintSampleParams("CalculateTradingCommissions", loginParams, sampleParams);

                session = O2GTransport.createSession();
                session.useTableManager(O2GTableManagerMode.Yes, null);
                statusListener = new SessionStatusListener(session);
                session.subscribeSessionStatus(statusListener);
                statusListener.Reset();
                session.login(loginParams.Login, loginParams.Password, loginParams.URL, loginParams.Connection);
                if (statusListener.WaitEvents() && statusListener.Connected)
                {
                    O2GTableManager tableManager = session.getTableManager();
                    O2GTableManagerStatus managerStatus = tableManager.getStatus();
                    while (managerStatus == O2GTableManagerStatus.TablesLoading)
                    {
                        Thread.Sleep(50);
                        managerStatus = tableManager.getStatus();
                    }

                    if (managerStatus == O2GTableManagerStatus.TablesLoadFailed)
                    {
                        throw new Exception("Cannot refresh all tables of table manager");
                    }
                    O2GAccountTableRow account = GetAccount(tableManager, sampleParams.AccountID);
                    if (account == null)
                        throw new Exception(string.IsNullOrEmpty(sampleParams.AccountID) ? "No valid accounts" : string.Format("The account '{0}' is not valid", sampleParams.AccountID));

                    O2GOfferTableRow offer = GetOffer(tableManager, sampleParams.Instrument);
                    if (offer == null)                    
                        throw new Exception(string.Format("The instrument '{0}' is not valid", sampleParams.Instrument));                    

                    O2GLoginRules loginRules = session.getLoginRules();
                    if (loginRules == null)                    
                        throw new Exception("Cannot get login rules");
                    
                    O2GTradingSettingsProvider tradingSettingsProvider = loginRules.getTradingSettingsProvider();
                    int iBaseUnitSize = tradingSettingsProvider.getBaseUnitSize(sampleParams.Instrument, account);
                    int iAmount = iBaseUnitSize * sampleParams.Lots;

                    printEstimatedTradingCommissions(session, offer, account, iAmount, sampleParams.BuySell);
                    Console.WriteLine("Done!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.ToString());
            }
            finally
            {
                if (session != null)
                {
                    if (statusListener.Connected)
                    {
                        statusListener.Reset();
                        session.logout();
                        statusListener.WaitEvents();
                    }
                    session.unsubscribeSessionStatus(statusListener);
                    session.Dispose();
                }
            }
        }

        /// <summary>
        /// Print estimates trading commissions
        /// </summary>      
        private static void printEstimatedTradingCommissions(O2GSession session, O2GOfferTableRow offer, O2GAccountTableRow account, int iAmount, string sBuySell)
        {   
            //wait until commissions related information will be loaded
            O2GCommissionsProvider commissionProvider = session.getCommissionsProvider();
            while (commissionProvider.getStatus() == O2GCommissionStatus.CommissionStatusLoading)
                System.Threading.Thread.Sleep(1000);

            if (commissionProvider.getStatus() != O2GCommissionStatus.CommissionStatusReady)
                throw new Exception("Could not calculate the estimated commissions.");   
            
            //calculate commissions
            Console.WriteLine("Commission for open the position is {0}.", commissionProvider.calcOpenCommission(offer, account, iAmount, sBuySell, 0));
            Console.WriteLine("Commission for close the position is {0}.", commissionProvider.calcCloseCommission(offer, account, iAmount, sBuySell, 0));
            Console.WriteLine("Total commission for open and close the position is {0}.", commissionProvider.calcTotalCommission(offer, account, iAmount, sBuySell, 0, 0));
        }

        /// <summary>
        /// Find valid account
        /// </summary>
        /// <param name="tableManager"></param>
        /// <returns>account</returns>
        private static O2GAccountTableRow GetAccount(O2GTableManager tableManager, string sAccountID)
        {
            bool bHasAccount = false;
            O2GAccountTableRow account = null;
            O2GAccountsTable accountsTable = (O2GAccountsTable)tableManager.getTable(O2GTableType.Accounts);
            for (int i = 0; i < accountsTable.Count; i++)
            {
                account = accountsTable.getRow(i);
                string sAccountKind = account.AccountKind;
                if (sAccountKind.Equals("32") || sAccountKind.Equals("36"))
                {
                    if (account.MarginCallFlag.Equals("N"))
                    {
                        if (string.IsNullOrEmpty(sAccountID) || sAccountID.Equals(account.AccountID))
                        {
                            bHasAccount = true;
                            break;
                        }
                    }
                }
            }
            if (!bHasAccount)
            {
                return null;
            }
            else
            {
                return account;
            }
        }

        /// <summary>
        /// Find valid offer by instrument name
        /// </summary>
        /// <param name="tableManager"></param>
        /// <param name="sInstrument"></param>
        /// <returns>offer</returns>
        private static O2GOfferTableRow GetOffer(O2GTableManager tableManager, string sInstrument)
        {
            bool bHasOffer = false;
            O2GOfferTableRow offer = null;
            O2GOffersTable offersTable = (O2GOffersTable)tableManager.getTable(O2GTableType.Offers);
            for (int i = 0; i < offersTable.Count; i++)
            {
                offer = offersTable.getRow(i);
                if (offer.Instrument.Equals(sInstrument))
                {
                    if (offer.SubscriptionStatus.Equals("T"))
                    {
                        bHasOffer = true;
                        break;
                    }
                }
            }
            if (!bHasOffer)
            {
                return null;
            }
            else
            {
                return offer;
            }
        }

        /// <summary>
        /// Print process name and sample parameters
        /// </summary>
        /// <param name="procName"></param>
        /// <param name="loginPrm"></param>
        /// <param name="prm"></param>
        private static void PrintSampleParams(string procName, LoginParams loginPrm, SampleParams prm)
        {
            Console.WriteLine("{0}: Instrument='{1}', AccountID='{2}'", procName, prm.Instrument, prm.AccountID);
        }

        class LoginParams
        {
            public string Login
            {
                get
                {
                    return mLogin;
                }
            }
            private string mLogin;

            public string Password
            {
                get
                {
                    return mPassword;
                }
            }
            private string mPassword;

            public string URL
            {
                get
                {
                    return mURL;
                }
            }
            private string mURL;

            public string Connection
            {
                get
                {
                    return mConnection;
                }
            }
            private string mConnection;

            public string SessionID
            {
                get
                {
                    return mSessionID;
                }
            }
            private string mSessionID;

            public string Pin
            {
                get
                {
                    return mPin;
                }
            }
            private string mPin;

            /// <summary>
            /// ctor
            /// </summary>
            /// <param name="args"></param>
            public LoginParams(NameValueCollection args)
            {
                mLogin = GetRequiredArgument(args, "Login");
                mPassword = GetRequiredArgument(args, "Password");
                mURL = GetRequiredArgument(args, "URL");
                if (!string.IsNullOrEmpty(mURL))
                {
                    if (!mURL.EndsWith("Hosts.jsp", StringComparison.OrdinalIgnoreCase))
                    {
                        mURL += "/Hosts.jsp";
                    }
                }
                mConnection = GetRequiredArgument(args, "Connection");
                mSessionID = args["SessionID"];
                mPin = args["Pin"];
            }

            /// <summary>
            /// Get required argument from configuration file
            /// </summary>
            /// <param name="args">Configuration file key-value collection</param>
            /// <param name="sArgumentName">Argument name (key) from configuration file</param>
            /// <returns>Argument value</returns>
            private string GetRequiredArgument(NameValueCollection args, string sArgumentName)
            {
                string sArgument = args[sArgumentName];
                if (!string.IsNullOrEmpty(sArgument))
                {
                    sArgument = sArgument.Trim();
                }
                if (string.IsNullOrEmpty(sArgument))
                {
                    throw new Exception(string.Format("Please provide {0} in configuration file", sArgumentName));
                }
                return sArgument;
            }
        }

        class SampleParams
        {
            public string AccountID
            {
                get
                {
                    return mAccountID;
                }
                set
                {
                    mAccountID = value;
                }
            }
            private string mAccountID;

            public string Instrument
            {
                get
                {
                    return mInstrument;
                }
                set
                {
                    mInstrument = value;
                }
            }
            private string mInstrument;

            public string BuySell
            {
                get
                {
                    return mBuySell;
                }
            }
            private string mBuySell;

            public int Lots
            {
                get
                {
                    return mLots;
                }
            }
            private int mLots;

            /// <summary>
            /// ctor
            /// </summary>
            /// <param name="args"></param>
            public SampleParams(NameValueCollection args)
            {
                mInstrument = GetRequiredArgument(args, "Instrument");
                mAccountID = args["Account"];
                mBuySell = GetRequiredArgument(args, "BuySell");
                string sLots = args["Lots"];
                if (string.IsNullOrEmpty(sLots))
                {
                    sLots = "1"; // default
                }
                Int32.TryParse(sLots, out mLots);
                if (mLots <= 0)
                {
                    throw new Exception(string.Format("\"Lots\" value {0} is invalid; please fix the value in the configuration file", sLots));
                }
            }

            /// <summary>
            /// Get required argument from configuration file
            /// </summary>
            /// <param name="args">Configuration file key-value collection</param>
            /// <param name="sArgumentName">Argument name (key) from configuration file</param>
            /// <returns>Argument value</returns>
            private string GetRequiredArgument(NameValueCollection args, string sArgumentName)
            {
                string sArgument = args[sArgumentName];
                if (!string.IsNullOrEmpty(sArgument))
                {
                    sArgument = sArgument.Trim();
                }
                if (string.IsNullOrEmpty(sArgument))
                {
                    throw new Exception(string.Format("Please provide {0} in configuration file", sArgumentName));
                }
                return sArgument;
            }
        }

    }
}
