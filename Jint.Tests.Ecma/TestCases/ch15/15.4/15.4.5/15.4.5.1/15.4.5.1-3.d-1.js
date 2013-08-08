/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-3.d-1.js
 * @description Throw RangeError if attempt to set array length property to 4294967296 (2**32)
 */


function testcase() {
  try {
      [].length = 4294967296 ;
  } catch (e) {
	if (e instanceof RangeError) return true;
  }
 }
runTestCase(testcase);
