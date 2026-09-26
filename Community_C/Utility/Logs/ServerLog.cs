namespace Community_C.Utility.Logs;
/*
 * 목적:
 * - 서버에서 사용하는 구조화 로그 이벤트를 한곳에서 정의한다.
 * - HTTP 요청 결과, DB 연결 상태, OAuth 오류, 주요 사용자 동작을 기록한다.
 * - 실제 출력은 ILogger와 등록된 Logging Provider가 담당한다.
 *
 * HTTP 요청 처리:
 * - 200~399: 정상 처리 로그.
 * - 400~499: 요청 거부/클라이언트 오류 로그.
 * - 500 이상: 서버 오류 로그.
 * - 처리되지 않은 요청 예외와 요청 취소 감지는 ServerRequestLoggingMiddleware.cs에서 담당한다.
 *
 * ===== 속성(LoggerMessage) =====
 *
 * == EventId:
 *   로그 이벤트를 구분하는 고유 번호. 같은 종류의 로그를 검색하거나 모니터링 용도.
 *
 *   현재 EventId 구분:
 *   - 1000번대: HTTP 요청/응답.
 *   - 1100번대: 데이터베이스 상태.
 *   - 1200번대: OAuth 통신 및 응답.
 *   - 1300번대: 게시판, 회원가입 등 애플리케이션 동작.
 *
 * == Level:
 *   로그의 중요도와 심각도
 *
 *   - LogLevel.Information:
 *     정상 요청 완료, DB 연결 성공, 회원가입 완료 등 정상적인 운영 흐름을 기록.
 *
 *   - LogLevel.Warning:
 *     잘못된 요청, 400번대 응답, OAuth 외부 요청 실패처럼 애플리케이션은 계속 실행할 수 있지만 확인이 필요한 상황을 기록.
 *
 *   - LogLevel.Error:
 *     500번대 응답, 처리되지 않은 예외, DB 확인 실패처럼 정상적인 처리를 완료하지 못한 상황을 기록.
 *
 * == Message:
 *   실제 로그에 출력될 메시지 템플릿이다.
 *
 *   예:
 *   "HTTP {RequestMethod} {RequestPath} responded {StatusCode}"
 *
 * ===== 메서드 매개변수=====
 *
 * - ILogger logger:
 *   로그를 실제 Logging Provider로 전달한다.
 *
 * - Exception exception:
 *   예외 객체와 stack trace를 함께 기록, Exception 매개변수는 Message 템플릿에 직접 작성하지 않아도 됨.
 *
 * - 기타 매개변수:
 *   LoggerMessage의 Message 템플릿에 있는 이름과 연결되는 구조화 로그 값이다.
 *
 * ===== 동작 방식=====
 *
 * - ServerLog는 static partial 클래스로 선언되어 있으므로 객체를 생성하지 않고 사용.
 * - [LoggerMessage]가 붙은 partial 메서드는 빌드 시 자동 생성.
 * - 호출부에서는 필요한 ILogger와 로그 데이터를 전달.
 *
 * 호출 예시:
 *
 * 1. HTTP 요청 완료: ServerLog.RequestCompleted
 * 2. 처리되지 않은 요청 예외: ServerLog.UnhandledRequestException
 * 3. DB 연결 성공: ServerLog.DatabaseReady
 * 4. OAuth 원격 요청 실패: ServerLog.OAuthRemoteRequestFailed
 *
 * 로그 출력 흐름:
 *
 * 1. 호출부(사용자 동작)
 * 2. ServerLog의 LoggerMessage 메서드
 * 3. ILogger
 * 4. 등록된 Logging Provider
 * 5. JSON 콘솔과 일자별 JSON 로그 파일 출력
 */

internal static partial class ServerLog
{
    /// <summary>
    /// HTTP 요청/응답 로그 출력. 요청이 정상적으로 처리되었음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="requestMethod">HTTP 요청 메서드</param>
    /// <param name="requestPath">HTTP 요청 경로</param>
    /// <param name="statusCode">HTTP 응답 코드</param>
    /// <param name="elapsedMilliseconds">응답시간</param>
    /// <param name="correlationId">HTTP 요청 추적 식별자</param>
    /// <param name="userId">인증된 내부 사용자 ID 또는 anonymous</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Http.RequestCompleted,
        Level = LogLevel.Information,
        Message = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}, UserId: {UserId}")]
    public static partial void RequestCompleted(
        ILogger logger,
        string requestMethod,
        string requestPath,
        int statusCode,
        double elapsedMilliseconds,
        string correlationId,
        string userId);
    /// <summary>
    /// HTTP 요청/응답 로그 출력. 요청이 거부되었음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="requestMethod">HTTP 요청 메서드</param>
    /// <param name="requestPath">HTTP 요청 경로</param>
    /// <param name="statusCode">HTTP 응답 코드</param>
    /// <param name="elapsedMilliseconds">응답시간</param>
    /// <param name="correlationId">HTTP 요청 추적 식별자</param>
    /// <param name="userId">인증된 내부 사용자 ID 또는 anonymous</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Http.RequestRejected,
        Level = LogLevel.Warning,
        Message = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}, UserId: {UserId}")]
    public static partial void RequestRejected(
        ILogger logger,
        string requestMethod,
        string requestPath,
        int statusCode,
        double elapsedMilliseconds,
        string correlationId,
        string userId);
    /// <summary>
    /// HTTP 요청/응답 로그 출력. 요청 처리 중 오류가 발생했음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="requestMethod">HTTP 요청 메서드</param>
    /// <param name="requestPath">HTTP 요청 경로</param>
    /// <param name="statusCode">HTTP 응답 코드</param>
    /// <param name="elapsedMilliseconds">응답시간</param>
    /// <param name="correlationId">HTTP 요청 추적 식별자</param>
    /// <param name="userId">인증된 내부 사용자 ID 또는 anonymous</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Http.RequestFailed,
        Level = LogLevel.Error,
        Message = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}, UserId: {UserId}")]
    public static partial void RequestFailed(
        ILogger logger,
        string requestMethod,
        string requestPath,
        int statusCode,
        double elapsedMilliseconds,
        string correlationId,
        string userId);
    /// <summary>
    /// HTTP 요청 처리 중 처리되지 않은 예외가 발생했음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="exception">발생한 예외</param>
    /// <param name="requestMethod">HTTP 요청 메서드</param>
    /// <param name="requestPath">HTTP 요청 경로</param>
    /// <param name="correlationId">HTTP 요청 추적 식별자</param>
    /// <param name="userId">인증된 내부 사용자 ID 또는 anonymous</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Http.UnhandledException,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred during HTTP {RequestMethod} {RequestPath}. CorrelationId: {CorrelationId}, UserId: {UserId}")]
    public static partial void UnhandledRequestException(
        ILogger logger,
        Exception exception,
        string requestMethod,
        string requestPath,
        string correlationId,
        string userId);
    /// <summary>
    /// HTTP 요청 처리 중 클라이언트가 요청을 취소했음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="requestMethod">HTTP 요청 메서드</param>
    /// <param name="requestPath">HTTP 요청 경로</param>
    /// <param name="elapsedMilliseconds">응답시간</param>
    /// <param name="correlationId">HTTP 요청 추적 식별자</param>
    /// <param name="userId">인증된 내부 사용자 ID 또는 anonymous</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Http.RequestCanceled,
        Level = LogLevel.Warning,
        Message = "HTTP {RequestMethod} {RequestPath} was canceled by the client after {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}, UserId: {UserId}")]
    public static partial void RequestCanceled(
        ILogger logger,
        string requestMethod,
        string requestPath,
        double elapsedMilliseconds,
        string correlationId,
        string userId);
    /// <summary>
    /// 데이터베이스 연결 확인이 성공했음을 나타낸다. BoardCount는 데이터베이스에서 조회된 게시판 수를 의미한다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="boardCount">게시판 수</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Database.Ready,
        Level = LogLevel.Information,
        Message = "Database connectivity check succeeded. BoardCount: {BoardCount}")]
    public static partial void DatabaseReady(ILogger logger, int boardCount);
    /// <summary>
    /// 데이터베이스 연결 확인이 실패했음을 나타낸다. 애플리케이션은 계속 시작되지만 데이터베이스가 사용 불가능한 상태임을 경고한다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Database.Unavailable,
        Level = LogLevel.Warning,
        Message = "Database connectivity check returned unavailable. The application will continue starting.")]
    public static partial void DatabaseUnavailable(ILogger logger);
    /// <summary>
    /// 데이터베이스 연결 확인 중 예외가 발생했음을 나타낸다. 애플리케이션은 계속 시작되지만 데이터베이스가 사용 불가능한 상태임을 경고한다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="exception">발생한 예외</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Database.CheckFailed,
        Level = LogLevel.Error,
        Message = "Database connectivity check failed. The application will continue starting.")]
    public static partial void DatabaseCheckFailed(ILogger logger, Exception exception);
    /// <summary>
    /// 예약된 DB 확인 작업에서 User 테이블의 전체 사용자 수를 정상적으로 조회했음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="userCount">조회된 전체 사용자 수</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Database.KeepAliveSucceeded,
        Level = LogLevel.Information,
        Message = "Scheduled database keep-alive succeeded. UserCount: {UserCount}")]
    public static partial void DatabaseKeepAliveSucceeded(
        ILogger logger,
        int userCount);
    /// <summary>
    /// 예약된 DB 확인 작업에서 User 테이블의 전체 사용자 수를 조회하지 못했음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="exception">발생한 예외</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Database.KeepAliveFailed,
        Level = LogLevel.Error,
        Message = "Scheduled database keep-alive failed. The next scheduled check will still run.")]
    public static partial void DatabaseKeepAliveFailed(
        ILogger logger,
        Exception exception);
    /// <summary>
    /// OAuth 원격 요청이 실패했음을 나타낸다. Provider는 OAuth 공급자 이름, Operation은 수행한 OAuth 작업, RemoteStatusCode는 원격 서버에서 반환한 HTTP 상태 코드를 의미한다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="provider">OAuth 공급자 이름</param>
    /// <param name="operation">수행한 OAuth 작업</param>
    /// <param name="remoteStatusCode">원격 서버에서 반환한 HTTP 상태 코드</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.OAuth.RemoteRequestFailed,
        Level = LogLevel.Warning,
        Message = "OAuth remote request failed. Provider: {Provider}, Operation: {Operation}, RemoteStatusCode: {RemoteStatusCode}")]
    public static partial void OAuthRemoteRequestFailed(
        ILogger logger,
        string provider,
        string operation,
        int? remoteStatusCode);
    /// <summary>
    /// OAuth 공급자 응답을 파싱할 수 없음을 나타낸다. Provider는 OAuth 공급자 이름, ExceptionType은 발생한 예외 유형을 의미한다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="provider">OAuth 공급자 이름</param>
    /// <param name="exceptionType">발생한 예외 유형</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.OAuth.ResponseInvalid,
        Level = LogLevel.Warning,
        Message = "OAuth provider response could not be parsed. Provider: {Provider}, ExceptionType: {ExceptionType}")]
    public static partial void OAuthResponseInvalid(
        ILogger logger,
        string provider,
        string exceptionType);
    /// <summary>
    /// 게시판 목록을 로드할 수 없음을 나타낸다. 애플리케이션은 계속 실행되지만 게시판 기능이 제한될 수 있음을 경고한다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="exception">발생한 예외</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Application.BoardListLoadFailed,
        Level = LogLevel.Error,
        Message = "The board list could not be loaded.")]
    public static partial void BoardListLoadFailed(ILogger logger, Exception exception);
    /// <summary>
    /// 회원가입이 성공적으로 완료되었음을 나타낸다.
    /// </summary>
    /// <param name="logger">Logging Provider</param>
    /// <param name="userId">가입을 완료한 내부 사용자 ID</param>
    /// <param name="correlationId">HTTP 요청 추적 식별자</param>
    [LoggerMessage(
        EventId = ServerLogEventIds.Application.LocalUserRegistered,
        Level = LogLevel.Information,
        Message = "A local user registration completed successfully. UserId: {UserId}, CorrelationId: {CorrelationId}")]
    public static partial void LocalUserRegistered(
        ILogger logger,
        string userId,
        string correlationId);
}
