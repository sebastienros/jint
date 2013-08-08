/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test is actually testing the [[Delete]] internal method (8.12.8). Since the
 * language provides no way to directly exercise [[Delete]], the tests are placed here.
 *
 * @path ch11/11.4/11.4.1/11.4.1-4.a-11.js
 * @description delete operator returns true on deleting arguments propterties(arguments.callee)
 */


function testcase() {
  function foo(a,b)
  {
    return (delete arguments.callee); 
  }
  var d = delete arguments.callee;
  if(d === true && arguments.callee === undefined)
    return true;
 }
runTestCase(testcase);
