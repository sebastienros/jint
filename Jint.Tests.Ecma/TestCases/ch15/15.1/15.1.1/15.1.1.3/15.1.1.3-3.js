/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-3.js
 * @description undefined is not writable, simple assignment should return the rval value (11.13.1-6)
 */

function testcase(){
  var newProperty = undefined = 42;
  return (newProperty === 42);
}

runTestCase(testcase);
