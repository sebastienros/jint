/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-3-8.js
 * @description arguments allowed as function identifier in non-strict function expression
 */


function testcase()
{
  try 
  {
    eval("(function arguments (){});");
    return true;
  }
  catch (e) {  }  
 }
runTestCase(testcase);
