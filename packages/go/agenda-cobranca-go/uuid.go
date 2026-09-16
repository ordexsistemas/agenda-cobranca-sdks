package agendacobranca

import (
	"crypto/rand"
	"encoding/binary"
	"encoding/hex"
	"time"
)

func gerarNonceUUIDv4() string {
	var raw [16]byte
	if _, err := rand.Read(raw[:]); err != nil {
		binary.BigEndian.PutUint64(raw[0:8], uint64(time.Now().UnixNano()))
	}
	raw[6] = (raw[6] & 0x0f) | 0x40
	raw[8] = (raw[8] & 0x3f) | 0x80
	return formatarUUID(raw[:])
}

func formatarUUID(raw []byte) string {
	hexed := hex.EncodeToString(raw)
	return hexed[0:8] + "-" + hexed[8:12] + "-" + hexed[12:16] + "-" + hexed[16:20] + "-" + hexed[20:32]
}
