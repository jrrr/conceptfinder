import subprocess

# path to the conceptfinder application directory
conceptfinder_path = './conceptfinder'

class concept_finder:
    def __init__(self):
        self.proc = subprocess.Popen(['dotnet', 'run'],
            cwd=conceptfinder_path,
            stdout=subprocess.PIPE, stdin=subprocess.PIPE)

    def _read(self):
        output = []
        num_lines = int(self.proc.stdout.readline().decode('utf-8').strip())
        for i in range(num_lines):
            output.append(self.proc.stdout.readline().decode('utf-8').strip())
        return output

    def extract_concepts(self, sentences):
        self.proc.stdin.write(
            ('x ' + str(len(sentences)) + '\n' + '\n'.join(sentences) + '\n')
                .encode('utf-8')
        )
        self.proc.stdin.flush()
        output = self._read()
        concepts, lengths = map(list, zip(*[l.split(' ') for l in output])) \
            if output else ([], [])
        return concepts, [int(l) for l in lengths]

    def encode_concepts(self, sentences):
        self.proc.stdin.write(
            ('e ' + str(len(sentences)) + '\n' + '\n'.join(sentences) + '\n')
                .encode('utf-8')
        )
        self.proc.stdin.flush()
        return self._read()
