/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-6.js
 * @description String.prototype.trim works for an String
 */


function testcase() {
  try
  {
    if(String.prototype.trim.call(new String()) == "")
      return true;
  }
  catch(e)
  {
  }
 }
runTestCase(testcase);
