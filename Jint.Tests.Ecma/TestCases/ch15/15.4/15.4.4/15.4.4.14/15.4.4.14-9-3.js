/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-3.js
 * @description Array.prototype.indexOf must return correct index(string)
 */


function testcase() {
  var obj = {toString:function (){return "false"}};
  var szFalse = "false";
  var a = new Array("false1",undefined,0,false,null,1,obj,0,szFalse, "false");
  if (a.indexOf("false") === 8)  //a[8]=szFalse
  {
    return true;
  }
 }
runTestCase(testcase);
