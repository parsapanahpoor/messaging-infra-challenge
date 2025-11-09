# messaging-infra-challenge
Design and implement a message-driven logging backbone using RabbitMQ that supports two concurrent patterns: (1) work-queue distribution for critical Error logs (exactly-one consumer per message, balanced), and (2) real-time fanout for Info logs (broadcast to all active subscribers).
