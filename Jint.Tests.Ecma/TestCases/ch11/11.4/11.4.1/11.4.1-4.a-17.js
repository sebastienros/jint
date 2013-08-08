/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test is actually testing the [[Delete]] internal method (8.12.8). Since the
 * language provides no way to directly exercise [[Delete]], the tests are placed here.
 *
 * @path ch11/11.4/11.4.1/11.4.1-4.a-17.js
 * @description delete operator returns true on deleting a arguments element
 */


function testcase() {
  function foo(a,b)
  {
    var d = delete arguments[0];
    return (d === true && arguments[0] === undefined);  
  }

  if(foo(1,2) === true)
    return true;
 }
runTestCase(testcase);
