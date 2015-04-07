#! perl.exe

# This scripts takes a CSV file were each line describes a task
# The list of tasks are to be performed during a progressive refinement experiment
# Format of the file: technique,strategy,model,annotation
# The script takes each line and individually puts it in task.csv, in the same directory.
# The Unity3D app takes its parameters for execution from this file.
# When the Unity3D app finishes executing, the next line in the file is processed.


open (LISTFILE, "<", $ARGV[0]);

$n = 1;

if ( $#ARGV+1 < 1 ){
	print "Usage: ./runTaskList.pl taskList.csv";
}

$filename = $ARGV[0];
my @fileParts = split('\.',$ARGV[0]);
$participantID = $fileParts[0];

if ( $#ARGV+1 > 1 ) {
	print "Starting from task $ARGV[1]\n";
	$n=$ARGV[1];
	for (my $i=0; $i<$n; $i++){
		<LISTFILE>;
	}
}

foreach $line (<LISTFILE>){
	open (TASKFILE, ">", "task.csv") or die "Could not open task.csv: $!";
	$line =~ s/\s+$//;
	$line .= ",".$participantID;
	print TASKFILE $line;
	close TASKFILE;

	my @values = split(',', $line); 
	my @taskParts = split('_', $values[3]);
	
	do{
		$satisfied = 1;

		print $n . "\t" . $values[1] . "\t" . $values[2] ;
		if (length($values[2])<8) { 
			print "\t\t";
		} else {
			print "\t";
		}
		
		print $taskParts[1] ;
		<STDIN>;

		`exp.exe`;

		print "\tENTER to continue or type Repeat : ";
		$_ = <STDIN>;
		if (/^[Rr]/) { $satisfied = 0; }
	}while($satisfied==0);
	$n++;
}

close LISTFILE;
