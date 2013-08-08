/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-2-1.js
 * @description eval allowed as formal parameter name of a non-strict function declaration
 */


function testcase()
{
  try 
  {
    eval("function foo(eval){};");
    return true;
  }
  catch (e) {  }
 }
runTestCase(testcase);
