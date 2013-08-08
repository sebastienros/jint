/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-2-2.js
 * @description delete operator returns true when deleting returned value from a function
 */


function testcase() {
  var bIsFooCalled = false;
  var foo = function(){bIsFooCalled = true;};

  var d = delete foo();
  if(d === true && bIsFooCalled === true)
    return true;
 }
runTestCase(testcase);
