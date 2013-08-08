/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.4/7.8.4-6-s.js
 * @description An OctalEscapeSequence is not allowed in a String under Strict Mode
 * @onlyStrict
 */


function testcase()
{
  try 
  {
    eval('"use strict"; var x = "\\01z";');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
