/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-1-s.js
 * @description Duplicate seperate parameter name in Function constructor throws SyntaxError in strict mode
 * @onlyStrict
 */


function testcase() {   
  try {
    Function('a','a','"use strict";');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
  
 }
runTestCase(testcase);
