#!/usr/bin/perl
# Usage: ./createTaskList.pl technique firstStrategy participantID

sub fisher_yates_shuffle {
	my $array = shift;
	my $i;
	for ($i = @$array; --$i; ) {
		my $j = int rand ($i+1);
		next if $i == $j;
		@$array[$i,$j] = @$array[$j,$i];
	}
}

if ( $#ARGV+1 < 3 ) { die "Not enough arguments: $!\n"; }

if ($ARGV[0] eq "sns") { $technique="Slice'n'Swipe"; }
elsif ($ARGV[0] eq "vsweep") { $technique="VolumeSweeper"; }
elsif ($ARGV[0] eq "lasso") { $technique="Lasso"; }
else { die "$ARGV[0] technique does not exist\n"; }

if ($ARGV[1] eq "fast") { $strategy="Fast"; }
elsif ($ARGV[1] eq "precise") { $strategy="Precise"; }
else { die "$ARGV[1] cannot be the first strategy\n"; }

$trainingTasks[0] = "None,Fast,LongHornBeetle,SliceNSwipe/LongHornBeetle_tail_2n.annotation.csv";
$trainingTasks[1] = "office2,LassoFast/Office2_whiteboard_ki.annotation.csv,training";
$trainingTasks[2] = "LongHornBeetle,SliceNSwipe/LongHornBeetle_head_ab.annotation.csv,training";

open TASKSFILE, "<", "$ARGV[0]Tasks.csv" or die $!;
chomp(@tasks = <TASKSFILE>);

$participantID = $ARGV[2];
open NEWCSV, ">", "p$participantID.csv" or die $!;
print NEWCSV $trainingTasks[0]."\n";

for (my $i = 0; $i < 3; $i++){
	if ($i == 1) {
		if ($strategy eq "Fast") { $strategy = "Precise"; }
		elsif ($strategy eq "Precise") { $strategy = "Fast"; }
	} elsif ($i == 2) {
		$strategy = "Both";
	}
	
	print NEWCSV $technique.",".$strategy.",".$trainingTasks[1]."\n";
	print NEWCSV $technique.",".$strategy.",".$trainingTasks[2]."\n";
	
	fisher_yates_shuffle(\@tasks);
	for (my $j = 0; $j < 6; $j++){
		print NEWCSV $technique.",".$strategy.",".$tasks[$j]."\n";
	}
}
