/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-3-s.js
 * @description Function constructor having a formal parameter named 'eval' throws SyntaxError if function body is strict mode
 * @onlyStrict
 */


function testcase() {
  

  try {
    Function('eval', '"use strict";');
	return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
