/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-2-5.js
 * @description arguments allowed as formal parameter name of a non-strict function declaration
 */


function testcase()
{
  try 
  {
    eval("function foo(arguments){};");
    return true;
  }
  catch (e) {  }
 }
runTestCase(testcase);
