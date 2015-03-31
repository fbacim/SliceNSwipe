#! perl.exe

# This scripts takes a CSV file were each line describes a task
# The list of tasks are to be performed during a progressive refinement experiment
# Format of the file: technique,strategy,model,annotation
# The script takes each line and individually puts it in task.csv, in the same directory.
# The Unity3D app takes its parameters for execution from this file.
# When the Unity3D app finishes executing, the next line in the file is processed.


open (LISTFILE, "<", $ARGV[1]);

$n = 1;

if ( $#ARGV+1 > 2 ) {
	print "Starting from task $ARGV[1]\n";
	$n=$ARGV[2];
	for (my $i=0; $i<$n; $i++){
		<LISTFILE>;
	}
}

foreach $line (<LISTFILE>){
	do{
		$satisfied = 1;
		open (TASKFILE, ">", "task.csv") or die "Could not open task.csv: $!";
		$line =~ s/\s+$//;
		$line .= ",".$ARGV[0];
		print TASKFILE $line;
		close TASKFILE;

		print $n . ": " . $line;
		`exp.exe`;

		print "\nRepeat last task [y/n]? ";
		$_ = <STDIN>;
		if (/y/) { $satisfied = 0; }
	}while($satisfied==0);
	$n++;
}

close LISTFILE;
