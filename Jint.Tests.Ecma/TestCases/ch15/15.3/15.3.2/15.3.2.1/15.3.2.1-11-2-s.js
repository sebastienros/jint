/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-2-s.js
 * @description Duplicate seperate parameter name in Function constructor called from strict mode allowed if body not strict
 * @onlyStrict
 */


function testcase()
{ 
  "use strict"; 
  try {
    Function('a','a','return;');
    return true;
  } catch (e) {
    return false;
  }
 }
runTestCase(testcase);
