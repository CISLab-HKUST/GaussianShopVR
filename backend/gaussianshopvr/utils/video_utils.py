import av


def seq2video(img_seq, file_path, w, h, fps):
    output = av.open(file_path, "w")
    stream = output.add_stream("libx264", rate=fps)
    stream.width = w
    stream.height = h
    stream.pix_fmt = "yuv420p"
    for img in img_seq:
        for packet in stream.encode(av.VideoFrame.from_ndarray(img, format="rgb24")):
            output.mux(packet)
    for packet in stream.encode():
        output.mux(packet)
    output.close()
