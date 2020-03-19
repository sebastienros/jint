function test() {
	let x = 9;
	assert(x === 9)
	test2();
}

function test2() {
	let error = false;
	try {
		assert(x !== 9)
	} catch(e) {
		error = true;
	}
	assert(error);
}

test();