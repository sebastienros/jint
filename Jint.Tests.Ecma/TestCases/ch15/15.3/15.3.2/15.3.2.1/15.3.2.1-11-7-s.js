/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-7-s.js
 * @description Function constructor call from strict code with formal parameter named arguments does not throws SyntaxError if function body is not strict mode
 * @onlyStrict
 */


function testcase() {
   "use strict";
   try {
     Function('arguments', 'return;');
     return true;
	 
   } catch (e) {
     return false;
   }
  }
runTestCase(testcase);
