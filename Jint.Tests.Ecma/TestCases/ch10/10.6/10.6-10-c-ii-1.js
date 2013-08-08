/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-10-c-ii-1.js
 * @description arguments[i] change with actual parameters
 */


function testcase() {
  function foo(a,b,c)
  {
    a = 1; b = 'str'; c = 2.1;
    if(arguments[0] === 1 && arguments[1] === 'str' && arguments[2] === 2.1)
      return true;   
  }
  return foo(10,'sss',1);
 }
runTestCase(testcase);
