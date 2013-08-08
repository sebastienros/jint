/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.4/7.8.4-33-s.js
 * @description Three OctalEscapeSequences in a String are not allowed in a String under Strict Mode
 * @onlyStrict
 */


function testcase()
{
  try 
  {
    eval('"use strict"; var x = "\\1\\2\\7";');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
