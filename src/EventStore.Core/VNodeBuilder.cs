using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Data;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Core.Index;
using EventStore.Core.Settings;
using ILogger = Serilog.ILogger;

namespace EventStore.Core {
	/// <summary>
	/// Allows a client to build a <see cref="ClusterVNode" /> for use with the Embedded client API by specifying
	/// high level options rather than using the constructor of <see cref="ClusterVNode"/> directly.
	/// </summary>
	public abstract class VNodeBuilder {
		// ReSharper disable FieldCanBeMadeReadOnly.Local - as more options are added
		protected ILogger _log;

		protected int _chunkSize;
		protected string _dbPath;
		protected long _chunksCacheSize;
		protected int _cachedChunks;
		protected bool _inMemoryDb;
		protected bool _startStandardProjections;
		protected bool _disableHTTPCaching;
		protected bool _logHttpRequests;
		protected bool _enableHistograms;
		protected bool _enableAtomPubOverHTTP;
		protected IPEndPoint _internalTcp;
		protected IPEndPoint _internalSecureTcp;
		protected IPEndPoint _externalTcp;
		protected IPEndPoint _externalSecureTcp;
		protected IPEndPoint _internalHttp;
		protected IPEndPoint _externalHttp;

		protected bool _enableTrustedAuth;
		protected X509Certificate2 _certificate;
		protected int _workerThreads;

		protected bool _discoverViaDns;
		protected string _clusterDns;
		protected List<IPEndPoint> _gossipSeeds;

		protected TimeSpan _minFlushDelay;

		protected int _clusterNodeCount;
		protected int _prepareAckCount;
		protected int _commitAckCount;
		protected TimeSpan _prepareTimeout;
		protected TimeSpan _commitTimeout;
		protected TimeSpan _writeTimeout;

		protected int _nodePriority;

		protected bool _enableExternalTCP;
		protected bool _disableInternalTls;
		protected bool _disableExternalTls;

		protected TimeSpan _statsPeriod;
		protected StatsStorage _statsStorage;

		protected AuthenticationProviderFactory _authenticationProviderFactory;
		protected bool _disableFirstLevelHttpAuthorization;
		protected bool _logFailedAuthenticationAttempts;
		protected bool _disableScavengeMerging;
		protected int _scavengeHistoryMaxAge;
		protected bool _adminOnPublic;
		protected bool _statsOnPublic;
		protected bool _gossipOnPublic;
		protected TimeSpan _gossipInterval;
		protected TimeSpan _gossipAllowedTimeDifference;
		protected TimeSpan _deadMemberRemovalPeriod;
		protected TimeSpan _gossipTimeout;
		protected GossipAdvertiseInfo _gossipAdvertiseInfo;

		protected TimeSpan _intTcpHeartbeatTimeout;
		protected TimeSpan _intTcpHeartbeatInterval;
		protected TimeSpan _extTcpHeartbeatTimeout;
		protected TimeSpan _extTcpHeartbeatInterval;
		protected int _connectionPendingSendBytesThreshold;
		protected int _connectionQueueSizeThreshold;

		protected bool _skipVerifyDbHashes;
		protected int _maxMemtableSize;
		protected int _hashCollisionReadLimit;
		protected List<ISubsystem> _subsystems;
		protected int _clusterGossipPort;
		protected int _readerThreadsCount;
		protected bool _unbuffered;
		protected bool _writethrough;
		protected int _chunkInitialReaderCount;

		protected string _index;
		protected bool _skipIndexVerify;
		protected int _indexCacheDepth;
		protected bool _optimizeIndexMerge;

		protected bool _unsafeIgnoreHardDelete;
		protected bool _unsafeDisableFlushToDisk;
		protected ProjectionType _projectionType;
		protected int _projectionsThreads;
		protected TimeSpan _projectionsQueryExpiry;
		protected bool _faultOutOfOrderProjections;

		protected TFChunkDb _db;
		protected ClusterVNodeSettings _vNodeSettings;
		protected TFChunkDbConfig _dbConfig;
		private IPAddress _advertiseInternalIPAs;
		private IPAddress _advertiseExternalIPAs;
		private int _advertiseInternalHttpPortAs;
		private int _advertiseExternalHttpPortAs;
		private int _advertiseInternalSecureTcpPortAs;
		private int _advertiseExternalSecureTcpPortAs;
		private int _advertiseInternalTcpPortAs;
		private int _advertiseExternalTcpPortAs;
		protected byte _indexBitnessVersion;
		protected bool _alwaysKeepScavenged;
		protected bool _skipIndexScanOnReads;
		private bool _reduceFileCachePressure;
		private int _initializationThreads;
		private int _maxAutoMergeIndexLevel;
		private int _maxAppendSize;

		private bool _gossipOnSingleNode;

		private bool _readOnlyReplica;
		private bool _unsafeAllowSurplusNodes;
		private Func<HttpMessageHandler> _createHttpMessageHandler;
		private AuthorizationProviderFactory _authorizationProviderFactory;

		// ReSharper restore FieldCanBeMadeReadOnly.Local

		protected VNodeBuilder() {
			_log = Serilog.Log.ForContext<VNodeBuilder>();
			_statsStorage = StatsStorage.Stream;

			_chunkSize = TFConsts.ChunkSize;
			_dbPath = Path.Combine(Path.GetTempPath(), "EmbeddedEventStore",
				string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-EmbeddedNode", DateTime.UtcNow));
			_chunksCacheSize = TFConsts.ChunksCacheSize;
			_cachedChunks = Opts.CachedChunksDefault;
			_inMemoryDb = true;
			_projectionType = ProjectionType.None;

			_enableAtomPubOverHTTP = Opts.EnableAtomPubOverHTTPDefault;
			_externalTcp = new IPEndPoint(Opts.ExternalIpDefault, Opts.ExternalTcpPortDefault);
			_externalSecureTcp = null;
			_internalTcp = new IPEndPoint(Opts.InternalIpDefault, Opts.InternalTcpPortDefault);
			_internalSecureTcp = null;
			_externalHttp = new IPEndPoint(Opts.ExternalIpDefault, Opts.ExternalHttpPortDefault);
			_internalHttp = new IPEndPoint(Opts.InternalIpDefault, Opts.InternalHttpPortDefault);

			_enableTrustedAuth = Opts.EnableTrustedAuthDefault;
			_readerThreadsCount = Opts.ReaderThreadsCountDefault;
			_certificate = null;
			_workerThreads = Opts.WorkerThreadsDefault;

			_discoverViaDns = Opts.DiscoverViaDnsDefault;
			_clusterDns = Opts.ClusterDnsDefault;
			_gossipSeeds = new List<IPEndPoint>();

			_minFlushDelay = TimeSpan.FromMilliseconds(Opts.MinFlushDelayMsDefault);

			_clusterNodeCount = 1;
			_prepareAckCount = 1;
			_commitAckCount = 1;
			_prepareTimeout = TimeSpan.FromMilliseconds(Opts.PrepareTimeoutMsDefault);
			_commitTimeout = TimeSpan.FromMilliseconds(Opts.CommitTimeoutMsDefault);
			_writeTimeout = TimeSpan.FromMilliseconds(Opts.WriteTimeoutMsDefault);

			_nodePriority = Opts.NodePriorityDefault;

			_disableInternalTls = Opts.DisableInternalTlsDefault;
			_disableExternalTls = Opts.DisableExternalTlsDefault;
			_enableExternalTCP = Opts.EnableExternalTCPDefault;

			_statsPeriod = TimeSpan.FromSeconds(Opts.StatsPeriodDefault);
			
			_authenticationProviderFactory = new AuthenticationProviderFactory(components => 
				new InternalAuthenticationProviderFactory(components));

			_disableFirstLevelHttpAuthorization = Opts.DisableFirstLevelHttpAuthorizationDefault;

			_authorizationProviderFactory = new AuthorizationProviderFactory(components =>
				new LegacyAuthorizationProviderFactory(components.MainQueue));

			_disableScavengeMerging = Opts.DisableScavengeMergeDefault;
			_scavengeHistoryMaxAge = Opts.ScavengeHistoryMaxAgeDefault;
			_adminOnPublic = Opts.AdminOnExtDefault;
			_statsOnPublic = Opts.StatsOnExtDefault;
			_gossipOnPublic = Opts.GossipOnExtDefault;
			_gossipInterval = TimeSpan.FromMilliseconds(Opts.GossipIntervalMsDefault);
			_gossipAllowedTimeDifference = TimeSpan.FromMilliseconds(Opts.GossipAllowedDifferenceMsDefault);
			_gossipTimeout = TimeSpan.FromMilliseconds(Opts.GossipTimeoutMsDefault);

			_intTcpHeartbeatInterval = TimeSpan.FromMilliseconds(Opts.IntTcpHeartbeatIntervalDefault);
			_intTcpHeartbeatTimeout = TimeSpan.FromMilliseconds(Opts.IntTcpHeartbeatTimeoutDefault);
			_extTcpHeartbeatInterval = TimeSpan.FromMilliseconds(Opts.ExtTcpHeartbeatIntervalDefault);
			_extTcpHeartbeatTimeout = TimeSpan.FromMilliseconds(Opts.ExtTcpHeartbeatTimeoutDefault);
			_connectionPendingSendBytesThreshold = Opts.ConnectionPendingSendBytesThresholdDefault;
			_connectionQueueSizeThreshold = Opts.ConnectionQueueSizeThresholdDefault;

			_skipVerifyDbHashes = Opts.SkipDbVerifyDefault;
			_maxMemtableSize = Opts.MaxMemtableSizeDefault;
			_subsystems = new List<ISubsystem>();
			_clusterGossipPort = Opts.ClusterGossipPortDefault;

			_startStandardProjections = Opts.StartStandardProjectionsDefault;
			_disableHTTPCaching = Opts.DisableHttpCachingDefault;
			_logHttpRequests = Opts.LogHttpRequestsDefault;
			_logFailedAuthenticationAttempts = Opts.LogFailedAuthenticationAttemptsDefault;
			_enableHistograms = Opts.LogHttpRequestsDefault;
			_index = null;
			_skipIndexVerify = Opts.SkipIndexVerifyDefault;
			_indexCacheDepth = Opts.IndexCacheDepthDefault;
			_indexBitnessVersion = Opts.IndexBitnessVersionDefault;
			_optimizeIndexMerge = Opts.OptimizeIndexMergeDefault;
			_unsafeIgnoreHardDelete = Opts.UnsafeIgnoreHardDeleteDefault;
			_unsafeDisableFlushToDisk = Opts.UnsafeDisableFlushToDiskDefault;
			_alwaysKeepScavenged = Opts.AlwaysKeepScavengedDefault;
			_skipIndexScanOnReads = Opts.SkipIndexScanOnReadsDefault;
			_chunkInitialReaderCount = Opts.ChunkInitialReaderCountDefault;
			_projectionsQueryExpiry = TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault);
			_faultOutOfOrderProjections = Opts.FaultOutOfOrderProjectionsDefault;
			_reduceFileCachePressure = Opts.ReduceFileCachePressureDefault;
			_initializationThreads = Opts.InitializationThreadsDefault;

			_readOnlyReplica = Opts.ReadOnlyReplicaDefault;
			_unsafeAllowSurplusNodes = Opts.UnsafeAllowSurplusNodesDefault;
			_maxAppendSize = Opts.MaxAppendSizeDefault;
			_deadMemberRemovalPeriod = TimeSpan.FromSeconds(Opts.DeadMemberRemovalPeriodDefault);
		}

		protected VNodeBuilder WithSingleNodeSettings() {
			_clusterNodeCount = 1;
			_prepareAckCount = 1;
			_commitAckCount = 1;
			return this;
		}

		protected VNodeBuilder WithClusterNodeSettings(int clusterNodeCount) {
			int quorumSize = clusterNodeCount / 2 + 1;
			_clusterNodeCount = clusterNodeCount;
			_prepareAckCount = quorumSize;
			_commitAckCount = quorumSize;
			return this;
		}

		/// <summary>
		/// Enable HTTP API Interface
		/// </summary>
		/// <param name="WithEnableAtomPubOverHTTP">Enable AtomPub over HTTP</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithEnableAtomPubOverHTTP(bool enableAtomPubOverHTTP) {
			_enableAtomPubOverHTTP = enableAtomPubOverHTTP;
			return this;
		}

		/// <summary>
		/// Start standard projections.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder StartStandardProjections() {
			_startStandardProjections = true;
			return this;
		}

		/// <summary>
		/// Disable HTTP Caching.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DisableHTTPCaching() {
			_disableHTTPCaching = true;
			return this;
		}

		/// <summary>
		/// Sets the mode and the number of threads on which to run projections.
		/// </summary>
		/// <param name="projectionType">The mode in which to run the projections system</param>
		/// <param name="numberOfThreads">The number of threads to use for projections. Defaults to 3.</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder RunProjections(ProjectionType projectionType,
			int numberOfThreads = Opts.ProjectionThreadsDefault,
			bool faultOutOfOrderProjections = Opts.FaultOutOfOrderProjectionsDefault) {
			_projectionType = projectionType;
			_projectionsThreads = numberOfThreads;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
			return this;
		}

		/// <summary>
		/// Sets how long a projection query can be idle before it expires.
		/// </summary>
		/// <param name="projectionQueryExpiry">The length of time a projection query can be idle before it expires.</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithProjectionQueryExpirationOf(TimeSpan projectionQueryExpiry) {
			_projectionsQueryExpiry = projectionQueryExpiry;
			return this;
		}


		/// <summary>
		/// Adds a custom subsystem to the builder. NOTE: This is an advanced use case that most people will never need!
		/// </summary>
		/// <param name="subsystem">The subsystem to add</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AddCustomSubsystem(ISubsystem subsystem) {
			_subsystems.Add(subsystem);
			return this;
		}

		/// <summary>
		/// Returns a builder set to run in memory only
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder RunInMemory() {
			_inMemoryDb = true;
			_dbPath = Path.Combine(Path.GetTempPath(), "EmbeddedEventStore",
				string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-EmbeddedNode", DateTime.UtcNow));
			return this;
		}

		/// <summary>
		/// Returns a builder set to write database files to the specified path
		/// </summary>
		/// <param name="path">The path on disk in which to write the database files</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder RunOnDisk(string path) {
			_inMemoryDb = false;
			_dbPath = path;
			return this;
		}

		/// <summary>
		/// Sets the default endpoints on localhost (1113 tcp, 2113 http)
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder OnDefaultEndpoints() {
			_internalHttp = new IPEndPoint(Opts.InternalIpDefault, 2112);
			_internalTcp = new IPEndPoint(Opts.InternalIpDefault, 1112);
			_externalHttp = new IPEndPoint(Opts.ExternalIpDefault, 2113);
			_externalTcp = new IPEndPoint(Opts.InternalIpDefault, 1113);
			return this;
		}

		/// <summary>
		/// Sets up the Internal IP that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseInternalIPAs(IPAddress intIpAdvertiseAs) {
			_advertiseInternalIPAs = intIpAdvertiseAs;
			return this;
		}

		/// <summary>
		/// Sets up the External IP that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseExternalIPAs(IPAddress extIpAdvertiseAs) {
			_advertiseExternalIPAs = extIpAdvertiseAs;
			return this;
		}

		/// <summary>
		/// Sets up the Internal Http Port that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseInternalHttpPortAs(int intHttpPortAdvertiseAs) {
			_advertiseInternalHttpPortAs = intHttpPortAdvertiseAs;
			return this;
		}


		/// <summary>
		/// Sets the number of reader threads to process read requests.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder HavingReaderThreads(int readerThreadsCount) {
			_readerThreadsCount = readerThreadsCount;
			return this;
		}


		/// <summary>
		/// Sets up the External Http Port that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseExternalHttpPortAs(int extHttpPortAdvertiseAs) {
			_advertiseExternalHttpPortAs = extHttpPortAdvertiseAs;
			return this;
		}

		/// <summary>
		/// Sets up the Internal Secure TCP Port that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseInternalSecureTCPPortAs(int intSecureTcpPortAdvertiseAs) {
			_advertiseInternalSecureTcpPortAs = intSecureTcpPortAdvertiseAs;
			return this;
		}

		/// <summary>
		/// Sets up the External Secure TCP Port that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseExternalSecureTCPPortAs(int extSecureTcpPortAdvertiseAs) {
			_advertiseExternalSecureTcpPortAs = extSecureTcpPortAdvertiseAs;
			return this;
		}

		/// <summary>
		/// Sets up the Internal TCP Port that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseInternalTCPPortAs(int intTcpPortAdvertiseAs) {
			_advertiseInternalTcpPortAs = intTcpPortAdvertiseAs;
			return this;
		}

		/// <summary>
		/// Enables gossip when running on a single node for testing purposes
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder GossipAsSingleNode() {
			_gossipOnSingleNode = true;
			return this;
		}


		/// <summary>
		/// Sets up the External TCP Port that would be advertised
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AdvertiseExternalTCPPortAs(int extTcpPortAdvertiseAs) {
			_advertiseExternalTcpPortAs = extTcpPortAdvertiseAs;
			return this;
		}


		/// <summary>
		/// Sets the internal gossip port (used when using cluster dns, this should point to a known port gossip will be running on)
		/// </summary>
		/// <param name="port">The cluster gossip to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithClusterGossipPort(int port) {
			_clusterGossipPort = port;
			return this;
		}

		/// <summary>
		/// Sets the internal http endpoint to the specified value
		/// </summary>
		/// <param name="endpoint">The internal endpoint to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithInternalHttpOn(IPEndPoint endpoint) {
			_internalHttp = endpoint;
			return this;
		}

		/// <summary>
		/// Sets the external http endpoint to the specified value
		/// </summary>
		/// <param name="endpoint">The external endpoint to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithExternalHttpOn(IPEndPoint endpoint) {
			_externalHttp = endpoint;
			return this;
		}

		/// <summary>
		/// Sets the internal tcp endpoint to the specified value
		/// </summary>
		/// <param name="endpoint">The internal endpoint to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithInternalTcpOn(IPEndPoint endpoint) {
			_internalTcp = endpoint;
			return this;
		}

		/// <summary>
		/// Sets the internal secure tcp endpoint to the specified value
		/// </summary>
		/// <param name="endpoint">The internal secure endpoint to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithInternalSecureTcpOn(IPEndPoint endpoint) {
			_internalSecureTcp = endpoint;
			return this;
		}

		/// <summary>
		/// Sets the external tcp endpoint to the specified value
		/// </summary>
		/// <param name="endpoint">The external endpoint to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithExternalTcpOn(IPEndPoint endpoint) {
			_externalTcp = endpoint;
			return this;
		}

		/// <summary>
		/// Sets the external secure tcp endpoint to the specified value
		/// </summary>
		/// <param name="endpoint">The external secure endpoint to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithExternalSecureTcpOn(IPEndPoint endpoint) {
			_externalSecureTcp = endpoint;
			return this;
		}

		/// <summary>
		/// Sets that TLS should be disabled on internal connections
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DisableInternalTls() {
			_disableInternalTls = true;
			return this;
		}

		/// Sets that TLS should be disabled on external connections
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DisableExternalTls() {
			_disableExternalTls = true;
			return this;
		}

		/// <summary>
		/// Enable External TCP Communication
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableExternalTCP() {
			_enableExternalTCP = true;
			return this;
		}

		/// <summary>
		/// Sets the gossip seeds this node should talk to
		/// </summary>
		/// <param name="endpoints">The gossip seeds this node should try to talk to</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithGossipSeeds(params IPEndPoint[] endpoints) {
			_gossipSeeds.Clear();
			_gossipSeeds.AddRange(endpoints);
			_discoverViaDns = false;
			return this;
		}

		/// <summary>
		/// Sets the maximum size a memtable is allowed to reach (in count) before being moved to be a ptable
		/// </summary>
		/// <param name="size">The maximum count</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder MaximumMemoryTableSizeOf(int size) {
			_maxMemtableSize = size;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of events to read in case of a stream Id hash collision
		/// </summary>
		/// <param name="hashCollisionReadLimit">The maximum count</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithHashCollisionReadLimitOf(int hashCollisionReadLimit) {
			_hashCollisionReadLimit = hashCollisionReadLimit;
			return this;
		}


		/// <summary>
		/// Marks that the existing database files should not be checked for checksums on startup.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DoNotVerifyDbHashes() {
			_skipVerifyDbHashes = true;
			return this;
		}

		/// <summary>
		/// Disables gossip on the public (client) interface
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder NoGossipOnPublicInterface() {
			_gossipOnPublic = false;
			return this;
		}

		/// <summary>
		/// Disables the admin interface on the public (client) interface
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder NoAdminOnPublicInterface() {
			_adminOnPublic = false;
			return this;
		}

		/// <summary>
		/// Disables statistics screens on the public (client) interface
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder NoStatsOnPublicInterface() {
			_statsOnPublic = false;
			return this;
		}

		/// <summary>
		/// Marks that the existing database files should be checked for checksums on startup.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder VerifyDbHashes() {
			_skipVerifyDbHashes = false;
			return this;
		}

		/// <summary>
		/// Sets the dns name used for the discovery of other cluster nodes
		/// </summary>
		/// <param name="name">The dns name the node should use to discover gossip partners</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithClusterDnsName(string name) {
			_clusterDns = name;
			_discoverViaDns = true;
			return this;
		}


		/// <summary>
		/// Disable dns discovery for the cluster
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DisableDnsDiscovery() {
			_discoverViaDns = false;
			return this;
		}

		/// <summary>
		/// Sets the number of worker threads to use in shared threadpool
		/// </summary>
		/// <param name="count">The number of worker threads</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithWorkerThreads(int count) {
			_workerThreads = count;
			return this;
		}
		
		/// <summary>
		/// Sets the Server TLS Certificate to be loaded from a file
		/// </summary>
		/// <param name="certificatePath">The path to the certificate file</param>
		/// <param name="privateKeyPath">The path to the private key file</param>
		/// <param name="password">The password for the certificate</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithServerCertificateFromFile(
			string certificatePath,
			string privateKeyPath,
			string password) {

			if (string.IsNullOrEmpty(privateKeyPath)) {
				try {
					_certificate = new X509Certificate2(certificatePath, password);
				} catch (CryptographicException exc) {
					throw new AggregateException("Error loading certificate file. Please verify that the correct password has been provided via the `CertificatePassword` option.", exc);
				}

				if (!_certificate.HasPrivateKey) {
					throw new Exception("Expect certificate to contain a private key. " +
					                    "Please either provide a certificate that contains one or set the private key" +
					                    " via the `CertificatePrivateKeyFile` option.");
				}
				return this;
			}
			
			var privateKey = Convert.FromBase64String(string.Join(string.Empty, File.ReadAllLines(privateKeyPath)
				.Skip(1)
				.SkipLast(1)));
			
			using var rsa = RSA.Create();
			rsa.ImportRSAPrivateKey(new ReadOnlySpan<byte>(privateKey), out _);
			
			using var publicCertificate = new X509Certificate2(certificatePath, password);
			using var publicWithPrivate = publicCertificate.CopyWithPrivateKey(rsa);
			
			_certificate = new X509Certificate2(publicWithPrivate.Export(X509ContentType.Pfx));
			return this;
		}

		/// <summary>
		/// Sets the heartbeat interval for the internal network interface.
		/// </summary>
		/// <param name="heartbeatInterval">The heartbeat interval</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithInternalHeartbeatInterval(TimeSpan heartbeatInterval) {
			_intTcpHeartbeatInterval = heartbeatInterval;
			return this;
		}

		/// <summary>
		/// Sets the heartbeat interval for the external network interface.
		/// </summary>
		/// <param name="heartbeatInterval">The heartbeat interval</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithExternalHeartbeatInterval(TimeSpan heartbeatInterval) {
			_extTcpHeartbeatInterval = heartbeatInterval;
			return this;
		}

		/// <summary>
		/// Sets the heartbeat timeout for the internal network interface.
		/// </summary>
		/// <param name="heartbeatTimeout">The heartbeat timeout</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithInternalHeartbeatTimeout(TimeSpan heartbeatTimeout) {
			_intTcpHeartbeatTimeout = heartbeatTimeout;
			return this;
		}

		/// <summary>
		/// Sets the heartbeat timeout for the external network interface.
		/// </summary>
		/// <param name="heartbeatTimeout">The heartbeat timeout</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithExternalHeartbeatTimeout(TimeSpan heartbeatTimeout) {
			_extTcpHeartbeatTimeout = heartbeatTimeout;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of pending send bytes allowed before a connection is closed.
		/// </summary>
		/// <param name="connectionPendingSendBytesThreshold">The number of pending send bytes allowed</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithConnectionPendingSendBytesThreshold(int connectionPendingSendBytesThreshold) {
			_connectionPendingSendBytesThreshold = connectionPendingSendBytesThreshold;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of connection operations allowed before a connection is closed.
		/// </summary>
		/// <param name="connectionQueueSizeThreshold">The number of connection operations allowed</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithConnectionQueueSizeThreshold(int connectionQueueSizeThreshold) {
			_connectionQueueSizeThreshold = connectionQueueSizeThreshold;
			return this;
		}

		/// <summary>
		/// Sets the gossip interval
		/// </summary>
		/// <param name="gossipInterval">The gossip interval</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithGossipInterval(TimeSpan gossipInterval) {
			_gossipInterval = gossipInterval;
			return this;
		}

		/// <summary>
		/// Sets the allowed gossip time difference
		/// </summary>
		/// <param name="gossipAllowedDifference">The allowed gossip time difference</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithGossipAllowedTimeDifference(TimeSpan gossipAllowedDifference) {
			_gossipAllowedTimeDifference = gossipAllowedDifference;
			return this;
		}
		

		/// <summary>
		/// Sets the period a dead node will remain in the gossip before being pruned
		/// </summary>
		/// <param name="deadMemberRemovalPeriod">The period a dead node will remain in the gossip before being pruned</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithDeadMemberRemovalPeriod(TimeSpan deadMemberRemovalPeriod) {
			_deadMemberRemovalPeriod = deadMemberRemovalPeriod;
			return this;
		}
		
		/// <summary>
		/// Sets the gossip timeout
		/// </summary>
		/// <param name="gossipTimeout">The gossip timeout</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithGossipTimeout(TimeSpan gossipTimeout) {
			_gossipTimeout = gossipTimeout;
			return this;
		}

		/// <summary>
		/// Sets the minimum flush delay
		/// </summary>
		/// <param name="minFlushDelay">The minimum flush delay</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithMinFlushDelay(TimeSpan minFlushDelay) {
			_minFlushDelay = minFlushDelay;
			return this;
		}

		/// <summary>
		/// Sets the prepare timeout
		/// </summary>
		/// <param name="prepareTimeout">The prepare timeout</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithPrepareTimeout(TimeSpan prepareTimeout) {
			_prepareTimeout = prepareTimeout;
			return this;
		}

		/// <summary>
		/// Sets the commit timeout
		/// </summary>
		/// <param name="commitTimeout">The commit timeout</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithCommitTimeout(TimeSpan commitTimeout) {
			_commitTimeout = commitTimeout;
			return this;
		}

		/// <summary>
		/// Sets the write timeout
		/// </summary>
		/// <param name="writeTimeout">The write timeout</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithWriteTimeout(TimeSpan writeTimeout) {
			_writeTimeout = writeTimeout;
			return this;
		}

		/// <summary>
		/// Sets the period between statistics gathers
		/// </summary>
		/// <param name="statsPeriod">The period between statistics gathers</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithStatsPeriod(TimeSpan statsPeriod) {
			_statsPeriod = statsPeriod;
			return this;
		}

		/// <summary>
		/// Sets how stats are stored. Default is Stream
		/// </summary>
		/// <param name="statsStorage">The storage method to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithStatsStorage(StatsStorage statsStorage) {
			_statsStorage = statsStorage;
			return this;
		}

		/// <summary>
		/// Sets the number of nodes which must acknowledge prepares.
		/// The minimum allowed value is one greater than half the cluster size.
		/// </summary>
		/// <param name="prepareCount">The number of nodes which must acknowledge prepares</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithPrepareCount(int prepareCount) {
			_prepareAckCount = prepareCount > _prepareAckCount ? prepareCount : _prepareAckCount;
			return this;
		}

		/// <summary>
		/// Sets the number of nodes which must acknowledge commits before acknowledging to a client.
		/// The minimum allowed value is one greater than half the cluster size.
		/// </summary>
		/// <param name="commitCount">The number of nodes which must acknowledge commits</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithCommitCount(int commitCount) {
			_commitAckCount = commitCount > _commitAckCount ? commitCount : _commitAckCount;
			return this;
		}

		/// <summary>
		/// Sets the node priority used during leader election
		/// </summary>
		/// <param name="nodePriority">The node priority used during leader election</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithNodePriority(int nodePriority) {
			_nodePriority = nodePriority;
			return this;
		}

		/// <summary>
		/// Disables the merging of chunks when scavenge is running
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DisableScavengeMerging() {
			_disableScavengeMerging = true;
			return this;
		}

		/// <summary>
		/// The number of days to keep scavenge history (Default: 30)
		/// </summary>
		/// <param name="scavengeHistoryMaxAge">The number of days to keep scavenge history</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithScavengeHistoryMaxAge(int scavengeHistoryMaxAge) {
			_scavengeHistoryMaxAge = scavengeHistoryMaxAge;
			return this;
		}

		/// <summary>
		/// Sets the path the index should be loaded/saved to
		/// </summary>
		/// <param name="indexPath">The index path</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithIndexPath(string indexPath) {
			_index = indexPath;
			return this;
		}

		/// <summary>
		/// Enable logging of Http Requests and Responses before they are processed
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableLoggingOfHttpRequests() {
			_logHttpRequests = true;
			return this;
		}
		
		/// <summary>
		/// Enable logging of Failed Authentication Attempts
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableLoggingOfFailedAuthenticationAttempts() {
			_logFailedAuthenticationAttempts = true;
			return this;
		}

		/// <summary>
		/// Enable the tracking of various histograms in the backend, typically only used for debugging
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableHistograms() {
			_enableHistograms = true;
			return this;
		}

		/// <summary>
		/// Skips verification of indexes on startup
		/// </summary>
		/// <param name="skipIndexVerify">Skips verification of indexes on startup</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithIndexVerification(bool skipIndexVerify) {
			_skipIndexVerify = skipIndexVerify;
			return this;
		}

		/// <summary>
		/// Sets the depth to cache for the mid point cache in index
		/// </summary>
		/// <param name="indexCacheDepth">The index cache depth</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithIndexCacheDepth(int indexCacheDepth) {
			_indexCacheDepth = indexCacheDepth;
			return this;
		}

		/// <summary>
		/// Optimizes index merges
		/// </summary>
		/// <param name="optimizeIndexMerge">Optimizes index merges</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithIndexMergeOptimization(bool optimizeIndexMerge) {
			_optimizeIndexMerge = optimizeIndexMerge;
			return this;
		}

		/// <summary>
		/// Disables Hard Deletes (UNSAFE: use to remove hard deletes)
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithUnsafeIgnoreHardDelete() {
			_unsafeIgnoreHardDelete = true;
			return this;
		}

		/// <summary>
		/// Disables Hard Deletes (UNSAFE: use to remove hard deletes)
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithUnsafeDisableFlushToDisk() {
			_unsafeDisableFlushToDisk = true;
			return this;
		}

		/// <summary>
		/// Enable trusted authentication by an intermediary in the HTTP
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableTrustedAuth() {
			_enableTrustedAuth = true;
			return this;
		}
		
		/// <summary>
		/// Sets the authorization provider factory to use
		/// </summary>
		/// <param name="authorizationProviderFactory">The authorization provider factory to use </param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithAuthorizationProvider(AuthorizationProviderFactory authorizationProviderFactory) {
			_authorizationProviderFactory = authorizationProviderFactory;
			return this;
		}

		/// <summary>
		/// Sets the authentication provider factory to use
		/// </summary>
		/// <param name="authenticationProviderFactory">The authentication provider factory to use </param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithAuthenticationProviderFactory(AuthenticationProviderFactory authenticationProviderFactory) {
			_authenticationProviderFactory = authenticationProviderFactory;
			return this;
		}

		/// <summary>
		/// Disables first level authorization checks on all HTTP endpoints.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder DisableFirstLevelHttpAuthorization() {
			_disableFirstLevelHttpAuthorization = true;
			return this;
		}

		/// <summary>
		/// Sets whether or not to use unbuffered/directio
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableUnbuffered() {
			_unbuffered = true;
			return this;
		}

		/// <summary>
		/// Sets whether or not to set the write-through flag on writes to the filesystem
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder EnableWriteThrough() {
			_writethrough = true;
			return this;
		}

		/// <summary>
		/// Sets the initial number of readers to open per TFChunk
		/// </summary>
		/// <param name="chunkInitialReaderCount">The initial number of readers to open per TFChunk</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithChunkInitialReaderCount(int chunkInitialReaderCount) {
			_chunkInitialReaderCount = chunkInitialReaderCount;
			return this;
		}


		/// <summary>
		/// Sets the number of threads to use to initialize the node.
		/// </summary>
		/// <param name="initializationThreads">The number of threads to use when initializing the node</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithInitializationThreads(int initializationThreads) {
			_initializationThreads = Math.Min(initializationThreads, Environment.ProcessorCount);
			return this;
		}

		/// <summary>
		/// Sets if we want the Merge Index operation to happen automatically
		/// </summary>
		/// <param name="maxAutoMergeIndexLevel">The value to set maxAutoMergeIndexLevel to do automatic index merges</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithMaxAutoMergeIndexLevel(int maxAutoMergeIndexLevel) {
			if (maxAutoMergeIndexLevel < 0)
				throw new ArgumentOutOfRangeException(nameof(maxAutoMergeIndexLevel), maxAutoMergeIndexLevel,
					"MaxAutoMergeIndexLevel must be > 0");
			_maxAutoMergeIndexLevel = maxAutoMergeIndexLevel;
			return this;
		}

		public VNodeBuilder WithMaxAppendSize(int maxAppendSize) {
			if (maxAppendSize <= 0) {
				throw new ArgumentOutOfRangeException(nameof(maxAppendSize), maxAppendSize, $"{nameof(maxAppendSize)} must be a positive number.");
			}
			if (maxAppendSize > 1024 * 1024 * 16) {
				throw new ArgumentOutOfRangeException(nameof(maxAppendSize), maxAppendSize, $"{nameof(maxAppendSize)} may not exceed 16MB.");
			}

			_maxAppendSize = maxAppendSize;
			return this;
		}

		/// <summary>
		/// Sets the Server TLS Certificate
		/// </summary>
		/// <param name="sslCertificate">The server TLS certificate to use</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithServerCertificate(X509Certificate2 sslCertificate) {
			_certificate = sslCertificate;
			return this;
		}

		/// <summary>
		/// Sets the Server TLS Certificate to be loaded from a certificate store
		/// </summary>
		/// <param name="storeLocation">The location of the certificate store</param>
		/// <param name="storeName">The name of the certificate store</param>
		/// <param name="certificateSubjectName">The subject name of the certificate</param>
		/// <param name="certificateThumbprint">The thumbpreint of the certificate</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithServerCertificateFromStore(StoreLocation storeLocation, StoreName storeName,
			string certificateSubjectName, string certificateThumbprint) {
			var store = new X509Store(storeName, storeLocation);
			_certificate = GetCertificateFromStore(store, certificateSubjectName, certificateThumbprint);
			return this;
		}

		/// <summary>
		/// Sets the Server TLS Certificate to be loaded from a certificate store
		/// </summary>
		/// <param name="storeName">The name of the certificate store</param>
		/// <param name="certificateSubjectName">The subject name of the certificate</param>
		/// <param name="certificateThumbprint">The thumbpreint of the certificate</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithServerCertificateFromStore(StoreName storeName, string certificateSubjectName,
			string certificateThumbprint) {
			var store = new X509Store(storeName);
			_certificate = GetCertificateFromStore(store, certificateSubjectName, certificateThumbprint);
			return this;
		}

		private X509Certificate2 GetCertificateFromStore(X509Store store, string certificateSubjectName,
			string certificateThumbprint) {
			try {
				store.Open(OpenFlags.OpenExistingOnly);
			} catch (Exception exc) {
				throw new Exception(
					string.Format("Could not open certificate store '{0}' in location {1}'.", store.Name,
						store.Location), exc);
			}

			if (!string.IsNullOrWhiteSpace(certificateThumbprint)) {
				var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, true);
				if (certificates.Count == 0)
					throw new Exception(string.Format("Could not find valid certificate with thumbprint '{0}'.",
						certificateThumbprint));

				//Can this even happen?
				if (certificates.Count > 1)
					throw new Exception(string.Format("Could not determine a unique certificate from thumbprint '{0}'.",
						certificateThumbprint));

				return certificates[0];
			}

			if (!string.IsNullOrWhiteSpace(certificateSubjectName)) {
				var certificates =
					store.Certificates.Find(X509FindType.FindBySubjectName, certificateSubjectName, true);
				if (certificates.Count == 0)
					throw new Exception(string.Format("Could not find valid certificate with subject name '{0}'.",
						certificateSubjectName));

				//Can this even happen?
				if (certificates.Count > 1)
					throw new Exception(string.Format(
						"Could not determine a unique certificate from subject name '{0}'.", certificateSubjectName));

				return certificates[0];
			}

			throw new ArgumentException(
				"No thumbprint or subject name was specified for a certificate, but a certificate store was specified.");
		}


		/// <summary>
		/// Sets the transaction file chunk size. Default is <see cref="TFConsts.ChunkSize"/>
		/// </summary>
		/// <param name="chunkSize">The size of the chunk, in bytes</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithTfChunkSize(int chunkSize) {
			_chunkSize = chunkSize;

			return this;
		}

		/// <summary>
		/// Sets the transaction file chunk cache size. Default is <see cref="TFConsts.ChunksCacheSize"/>
		/// </summary>
		/// <param name="chunksCacheSize">The size of the cache</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithTfChunksCacheSize(long chunksCacheSize) {
			_chunksCacheSize = chunksCacheSize;
			_cachedChunks = -1;

			return this;
		}

		/// <summary>
		/// The number of chunks to cache in unmanaged memory.
		/// </summary>
		/// <param name="cachedChunks">The number of chunks to cache</param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithTfCachedChunks(int cachedChunks) {
			_cachedChunks = cachedChunks;

			return this;
		}

		/// <summary>
		/// The bitness version of the indexes
		/// </summary>
		/// <param name="indexBitnessVersion">The version of the bitness <see cref="PTableVersions"/></param>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder WithIndexBitnessVersion(byte indexBitnessVersion) {
			_indexBitnessVersion = indexBitnessVersion;

			return this;
		}

		/// <summary>
		/// The newer chunks during a scavenge are always kept
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder AlwaysKeepScavenged() {
			_alwaysKeepScavenged = true;

			return this;
		}

		/// <summary>
		/// Skip index scan on reads.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder SkipIndexScanOnReads() {
			_skipIndexScanOnReads = true;

			return this;
		}

		/// <summary>
		/// Reduce file cache pressure by opening the DB chunks without RandomAccess hint.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set</returns>
		public VNodeBuilder ReduceFileCachePressure() {
			_reduceFileCachePressure = true;

			return this;
		}

		/// <summary>
		/// Sets this node as a read only replica that is not allowed to participate in elections.
		/// </summary>
		/// <returns></returns>
		public VNodeBuilder EnableReadOnlyReplica() {
			_readOnlyReplica = true;

			return this;
		}

		/// <summary>
		/// Determines the factory used to create the <see cref="HttpMessageHandler"/> used by internal http communications. Used for testing.
		/// </summary>
		/// <param name="createHttpMessageHandler">the <see cref="HttpMessageHandler"/> factory.</param>
		/// <returns></returns>
		public VNodeBuilder WithHttpMessageHandlerFactory(Func<HttpMessageHandler> createHttpMessageHandler) {
			_createHttpMessageHandler = createHttpMessageHandler;

			return this;
		}

		private GossipAdvertiseInfo EnsureGossipAdvertiseInfo() {
			if (_gossipAdvertiseInfo == null) {
				Ensure.Equal(false, _internalTcp == null && _internalSecureTcp == null, "Both internal TCP endpoints are null");

				IPAddress intIpAddress = _internalHttp.Address; //this value is just opts.IntIP
				IPAddress extIpAddress = _externalHttp.Address; //this value is just opts.ExtIP

				IPAddress intIpAddressToAdvertise = _advertiseInternalIPAs ?? intIpAddress;
				IPAddress extIpAddressToAdvertise = _advertiseExternalIPAs ?? extIpAddress;

				if (intIpAddress.Equals(IPAddress.Parse("0.0.0.0")) || extIpAddress.Equals(IPAddress.Parse("0.0.0.0"))) {
					IPAddress nonLoopbackAddress = IPFinder.GetNonLoopbackAddress();
					IPAddress addressToAdvertise = _clusterNodeCount > 1 ? nonLoopbackAddress : IPAddress.Loopback;

					if (intIpAddress.Equals(IPAddress.Parse("0.0.0.0")) && _advertiseInternalIPAs == null) {
						intIpAddressToAdvertise = addressToAdvertise;
					}

					if (extIpAddress.Equals(IPAddress.Parse("0.0.0.0")) && _advertiseExternalIPAs == null) {
						extIpAddressToAdvertise = addressToAdvertise;
					}
				}

				IPEndPoint intTcpEndPoint = null;
				if (_internalTcp != null) {
					var intTcpPort = _advertiseInternalTcpPortAs > 0 ? _advertiseInternalTcpPortAs : _internalTcp.Port;
					intTcpEndPoint = new IPEndPoint(intIpAddressToAdvertise, intTcpPort);
				}

				IPEndPoint intSecureTcpEndPoint = null;
				if (_internalSecureTcp != null) {
					var intSecureTcpPort = _advertiseInternalSecureTcpPortAs > 0 ? _advertiseInternalSecureTcpPortAs : _internalSecureTcp.Port;
					intSecureTcpEndPoint = new IPEndPoint(intIpAddressToAdvertise, intSecureTcpPort);
				}

				IPEndPoint extTcpEndPoint = null;
				if (_externalTcp != null) {
					int extTcpPort = _advertiseExternalTcpPortAs > 0 ? _advertiseExternalTcpPortAs : _externalTcp.Port;
					extTcpEndPoint = new IPEndPoint(extIpAddressToAdvertise, extTcpPort);
				}

				IPEndPoint extSecureTcpEndPoint = null;
				if (_externalSecureTcp != null) {
					int extSecureTcpPort = _advertiseExternalSecureTcpPortAs > 0 ? _advertiseExternalSecureTcpPortAs : _externalSecureTcp.Port;
					extSecureTcpEndPoint = new IPEndPoint(extIpAddressToAdvertise, extSecureTcpPort);
				}

				var intHttpPort = _advertiseInternalHttpPortAs > 0 ? _advertiseInternalHttpPortAs : _internalHttp.Port;
				var extHttpPort = _advertiseExternalHttpPortAs > 0 ? _advertiseExternalHttpPortAs : _externalHttp.Port;

				var intHttpEndPoint = new IPEndPoint(intIpAddressToAdvertise, intHttpPort);
				var extHttpEndPoint = new IPEndPoint(extIpAddressToAdvertise, extHttpPort);

				_gossipAdvertiseInfo = new GossipAdvertiseInfo(intTcpEndPoint, intSecureTcpEndPoint,
					extTcpEndPoint, extSecureTcpEndPoint,
					intHttpEndPoint, extHttpEndPoint,
					_advertiseInternalIPAs, _advertiseExternalIPAs,
					_advertiseInternalHttpPortAs, _advertiseExternalHttpPortAs);
			}

			return _gossipAdvertiseInfo;
		}

		protected abstract void SetUpProjectionsIfNeeded();

		/// <summary>
		/// Converts an <see cref="VNodeBuilder"/> to a <see cref="ClusterVNode"/>.
		/// </summary>
		/// <param name="builder"></param>
		/// <returns>A <see cref="ClusterVNode"/> built with the options that were set on the <see cref="VNodeBuilder"/></returns>
		public static implicit operator ClusterVNode(VNodeBuilder builder) {
			return builder.Build();
		}

		/// <summary>
		/// Converts an <see cref="VNodeBuilder"/> to a <see cref="ClusterVNode"/>.
		/// </summary>
		/// <param name="options">The options with which to build the infoController</param>
		/// <param name="consumerStrategies">The consumer strategies with which to build the node</param>
		/// <returns>A <see cref="ClusterVNode"/> built with the options that were set on the <see cref="VNodeBuilder"/></returns>
		public ClusterVNode Build(IOptions options = null,
			IPersistentSubscriptionConsumerStrategyFactory[] consumerStrategies = null) {
			SetUpProjectionsIfNeeded();
			_gossipAdvertiseInfo = EnsureGossipAdvertiseInfo();

			_dbConfig = CreateDbConfig(_chunkSize,
				_cachedChunks,
				_dbPath,
				_chunksCacheSize,
				_inMemoryDb,
				_unbuffered,
				_writethrough,
				_chunkInitialReaderCount,
				ComputeTFChunkMaxReaderCount(_chunkInitialReaderCount, _readerThreadsCount),
				_optimizeIndexMerge,
				_reduceFileCachePressure,
				_log);
			FileStreamExtensions.ConfigureFlush(disableFlushToDisk: _unsafeDisableFlushToDisk);

			_db = new TFChunkDb(_dbConfig);

			_vNodeSettings = new ClusterVNodeSettings(Guid.NewGuid(),
				0,
				_internalTcp,
				_internalSecureTcp,
				_externalTcp,
				_externalSecureTcp,
				_internalHttp,
				_externalHttp,
				_gossipAdvertiseInfo,
				_enableTrustedAuth,
				_certificate,
				_workerThreads,
				_discoverViaDns,
				_clusterDns,
				_gossipSeeds.ToArray(),
				_minFlushDelay,
				_clusterNodeCount,
				_prepareAckCount,
				_commitAckCount,
				_prepareTimeout,
				_commitTimeout,
				_writeTimeout,
				_disableInternalTls,
				_disableExternalTls,
				_statsPeriod,
				_statsStorage,
				_nodePriority,
				_authenticationProviderFactory,
				_authorizationProviderFactory,
				_disableScavengeMerging,
				_scavengeHistoryMaxAge,
				_adminOnPublic,
				_statsOnPublic,
				_gossipOnPublic,
				_gossipInterval,
				_gossipAllowedTimeDifference,
				_gossipTimeout,
				_intTcpHeartbeatTimeout,
				_intTcpHeartbeatInterval,
				_extTcpHeartbeatTimeout,
				_extTcpHeartbeatInterval,
				_deadMemberRemovalPeriod,
				!_skipVerifyDbHashes,
				_maxMemtableSize,
				_hashCollisionReadLimit,
				_startStandardProjections,
				_disableHTTPCaching,
				_logHttpRequests,
				_connectionPendingSendBytesThreshold,
				_connectionQueueSizeThreshold,
				ComputePTableMaxReaderCount(ESConsts.PTableInitialReaderCount, _readerThreadsCount),
				_index,
				_enableHistograms,
				_skipIndexVerify,
				_indexCacheDepth,
				_indexBitnessVersion,
				_optimizeIndexMerge,
				consumerStrategies,
				_unsafeIgnoreHardDelete,
				_readerThreadsCount,
				_alwaysKeepScavenged,
				_gossipOnSingleNode,
				_skipIndexScanOnReads,
				_reduceFileCachePressure,
				_initializationThreads,
				_faultOutOfOrderProjections,
				_maxAutoMergeIndexLevel,
				_disableFirstLevelHttpAuthorization,
				_logFailedAuthenticationAttempts,
				_readOnlyReplica,
				_maxAppendSize,
				_createHttpMessageHandler,
				_unsafeAllowSurplusNodes,
				_enableExternalTCP,
				_enableAtomPubOverHTTP);

			var infoController = new InfoController(options, _projectionType);

			var writerCheckpoint = _db.Config.WriterCheckpoint.Read();
			var chaserCheckpoint = _db.Config.ChaserCheckpoint.Read();
			var epochCheckpoint = _db.Config.EpochCheckpoint.Read();
			var truncateCheckpoint = _db.Config.TruncateCheckpoint.Read();

			_log.Information("{description,-25} {instanceId}", "INSTANCE ID:", _vNodeSettings.NodeInfo.InstanceId);
			_log.Information("{description,-25} {path}", "DATABASE:", _db.Config.Path);
			_log.Information("{description,-25} {writerCheckpoint} (0x{writerCheckpoint:X})", "WRITER CHECKPOINT:",
				writerCheckpoint, writerCheckpoint);
			_log.Information("{description,-25} {chaserCheckpoint} (0x{chaserCheckpoint:X})", "CHASER CHECKPOINT:",
				chaserCheckpoint, chaserCheckpoint);
			_log.Information("{description,-25} {epochCheckpoint} (0x{epochCheckpoint:X})", "EPOCH CHECKPOINT:",
				epochCheckpoint, epochCheckpoint);
			_log.Information("{description,-25} {truncateCheckpoint} (0x{truncateCheckpoint:X})", "TRUNCATE CHECKPOINT:",
				truncateCheckpoint, truncateCheckpoint);

			return new ClusterVNode(_db, _vNodeSettings, GetGossipSource(), infoController, _subsystems.ToArray());
		}

		private int ComputePTableMaxReaderCount(int ptableInitialReaderCount, int readerThreadsCount) {
			var ptableMaxReaderCount = 1 /* StorageWriter */
			                           + 1 /* StorageChaser */
			                           + 1 /* Projections */
			                           + TFChunkScavenger.MaxThreadCount /* Scavenging (1 per thread) */
			                           + 1 /* Subscription LinkTos resolving */
			                           + readerThreadsCount
			                           + 5 /* just in case reserve :) */;
			return Math.Max(ptableMaxReaderCount, ptableInitialReaderCount);
		}

		private int ComputeTFChunkMaxReaderCount(int tfChunkInitialReaderCount, int readerThreadsCount) {
			var tfChunkMaxReaderCount = ComputePTableMaxReaderCount(ESConsts.PTableInitialReaderCount, readerThreadsCount)
                                            + 2 /* for caching/uncaching, populating midpoints */
                                            + 1 /* for epoch manager usage of elections/replica service */
                                            + 1 /* for epoch manager usage of leader replication service */;
			return Math.Max(tfChunkMaxReaderCount, tfChunkInitialReaderCount);
		}


		private IGossipSeedSource GetGossipSource() {
			IGossipSeedSource gossipSeedSource;
			if (_discoverViaDns) {
				gossipSeedSource = new DnsGossipSeedSource(_clusterDns, _clusterGossipPort);
			} else {
				if ((_gossipSeeds == null || _gossipSeeds.Count == 0) && _clusterNodeCount > 1) {
					throw new Exception("DNS discovery is disabled, but no gossip seed endpoints have been specified. "
										+ "Specify gossip seeds using the `GossipSeed` option.");
				}

				if (_gossipSeeds == null)
					throw new ApplicationException("Gossip seeds cannot be null");
				gossipSeedSource = new KnownEndpointGossipSeedSource(_gossipSeeds.ToArray());
			}

			return gossipSeedSource;
		}

		private static TFChunkDbConfig CreateDbConfig(int chunkSize,
			int cachedChunks,
			string dbPath,
			long chunksCacheSize,
			bool inMemDb,
			bool unbuffered,
			bool writethrough,
			int chunkInitialReaderCount,
			int chunkMaxReaderCount,
			bool optimizeReadSideCache,
			bool reduceFileCachePressure,
			ILogger log) {
			ICheckpoint writerChk;
			ICheckpoint chaserChk;
			ICheckpoint epochChk;
			ICheckpoint truncateChk;
			//todo(clc) : promote these to file backed checkpoints re:project-io
			ICheckpoint replicationChk = new InMemoryCheckpoint(Checkpoint.Replication, initValue: -1);
			ICheckpoint indexChk = new InMemoryCheckpoint(Checkpoint.Replication, initValue: -1);
			if (inMemDb) {
				writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
				chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
				epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
				truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
			} else {
				try {
					if (!Directory.Exists(dbPath)) // mono crashes without this check
						Directory.CreateDirectory(dbPath);
				} catch (UnauthorizedAccessException) {
					if (dbPath == Locations.DefaultDataDirectory) {
						log.Information(
							"Access to path {dbPath} denied. The Event Store database will be created in {fallbackDefaultDataDirectory}",
							dbPath, Locations.FallbackDefaultDataDirectory);
						dbPath = Locations.FallbackDefaultDataDirectory;
						log.Information("Defaulting DB Path to {dbPath}", dbPath);

						if (!Directory.Exists(dbPath)) // mono crashes without this check
							Directory.CreateDirectory(dbPath);
					} else {
						throw;
					}
				}

				var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
				var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
				var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
				var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
				writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
				chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
				epochChk = new MemoryMappedFileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true,
					initValue: -1);
				truncateChk = new MemoryMappedFileCheckpoint(truncateCheckFilename, Checkpoint.Truncate,
					cached: true, initValue: -1);
			}

			var cache = cachedChunks >= 0
				? cachedChunks * (long)(TFConsts.ChunkSize + ChunkHeader.Size + ChunkFooter.Size)
				: chunksCacheSize;

			var nodeConfig = new TFChunkDbConfig(dbPath,
				new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
				chunkSize,
				cache,
				writerChk,
				chaserChk,
				epochChk,
				truncateChk,
				replicationChk,
				indexChk,
				chunkInitialReaderCount,
				chunkMaxReaderCount,
				inMemDb,
				unbuffered,
				writethrough,
				optimizeReadSideCache,
				reduceFileCachePressure);

			return nodeConfig;
		}

		public void WithUnsafeAllowSurplusNodes() {
			_unsafeAllowSurplusNodes = true;
		}
	}
}
