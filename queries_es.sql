create database eventstore;
use eventstore;

create table eventstream 
(
    stream_id bigint not null,
    event_name nvarchar(255) not null, 
    event_info nvarchar(max) not null,
    created_at datetime not null
)
--delete from eventstream
select * from eventstream where stream_id = 71


select * from eventstream where event_name = 'deposito' and stream_id = 71
select * from eventstream where event_name = 'saque' and stream_id = 71

select ISJSON(event_info) as JSON_VALIDO from eventstream where stream_id = 71

select JSON_VALUE(event_info, '$.QuantiaEmDinheiro') as QuantiaEmDinheiro from eventstream where stream_id = 71


