/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-6-s.js
 * @description Duplicate combined parameter name allowed in Function constructor called in strict mode if body not strict
 * @onlyStrict
 */


function testcase()
{ 
  "use strict"; 
  try {
     Function('a,a','return a;');
	 return true;
  } catch (e) {
     return false;
  }
  
 }
runTestCase(testcase);
