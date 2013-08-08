/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.2/10.4.2-2-c-1.js
 * @description Direct val code in non-strict mode - can instantiate variable in calling context
 */


function testcase() {
  var x = 0;
  return function inner() {
     eval("var x = 1");
     if (x === 1)
        return true;
     } ();
   }
runTestCase(testcase);
